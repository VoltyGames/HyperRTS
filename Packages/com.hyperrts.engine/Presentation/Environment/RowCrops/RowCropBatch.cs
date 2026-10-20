using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>One crop's GPU side for one camera: visible tiles, the (variant, band) buffers RowCrops.compute fills, and the indirect draws.</summary>
    internal sealed class RowCropBatch : IDisposable
    {
        // Matches RowCropInstance in RowCrops.compute and RowCropInput.hlsl.
        private const int InstanceStride = 48;
        private const int ThreadGroup = 64;
        private const int MinCapacity = 1024;

        // Headroom over an even split, as the hash never splits plants between variants exactly evenly.
        private const float VariantSlack = 1.15f;

        private static readonly int JobsId = Shader.PropertyToID("_Jobs");
        private static readonly int CountersId = Shader.PropertyToID("_Counters");
        private static readonly int CapacitiesId = Shader.PropertyToID("_Capacities");
        private static readonly int ArgsId = Shader.PropertyToID("_Args");
        private static readonly int SlotsId = Shader.PropertyToID("_Slots");
        private static readonly int SowingId = Shader.PropertyToID("_Sowing");
        private static readonly int FacesRowId = Shader.PropertyToID("_FacesRow");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int VarietyId = Shader.PropertyToID("_Variety");
        private static readonly int PatchId = Shader.PropertyToID("_Patch");
        private static readonly int VariantId = Shader.PropertyToID("_Variant");
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int CanopyHeightId = Shader.PropertyToID("_CanopyHeight");
        private static readonly int[] OutputIds =
            { Shader.PropertyToID("_Full"), Shader.PropertyToID("_Simple"), Shader.PropertyToID("_Card") };

        private readonly RowCrop _crop;
        private readonly RowCropKernels _kernels;
        private readonly int _variants;
        private readonly Mesh[] _meshes;
        private readonly GraphicsBuffer[] _instances;
        private readonly uint[] _capacities;
        private readonly MaterialPropertyBlock[] _blocks;
        private readonly int[] _needed = new int[RowCropView.Lods];
        private readonly Bounds[] _bands = new Bounds[RowCropView.Lods];
        private readonly List<RowCropJob> _pending = new();
        private readonly GraphicsBuffer _args;
        private readonly GraphicsBuffer _counters;
        private readonly GraphicsBuffer _capacityBuffer;
        private readonly int _plantBound;
        private GraphicsBuffer _jobs;
        private int _maxSpots;
        private bool _resized;

        public RowCropBatch(RowCrop crop, RowCropKernels kernels)
        {
            _crop = crop;
            _kernels = kernels;
            _variants = crop.variants.Count;
            _meshes = Meshes(crop);
            _instances = new GraphicsBuffer[_meshes.Length];
            _capacities = new uint[_meshes.Length];
            _blocks = new MaterialPropertyBlock[_meshes.Length];
            _plantBound = RowCropLattice.PlantBound(RowCropFields.TileSize, crop.rowSpacing, crop.plantSpacing);
            _counters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _meshes.Length, sizeof(uint));
            _counters.SetData(new uint[_meshes.Length]);
            _capacityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _meshes.Length, sizeof(uint));
            _args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, _meshes.Length,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _args.SetData(Commands(_meshes));
            for (var slot = 0; slot < _meshes.Length; slot++)
            {
                _blocks[slot] = new MaterialPropertyBlock();
                Resize(slot, MinCapacity);
            }
        }

        /// <summary>Whether the batch no longer draws <paramref name="crop"/> as it is: another crop, or its models changed.</summary>
        public bool Stale(RowCrop crop)
        {
            if (crop == null || crop != _crop || crop.variants.Count != _variants)
            {
                return true;
            }

            for (var slot = 0; slot < _meshes.Length; slot++)
            {
                var variant = crop.variants[slot / RowCropView.Lods];
                var mesh = variant?.Mesh(slot % RowCropView.Lods);
                if (mesh == null || mesh != _meshes[slot])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Forgets the last view's tiles.</summary>
        public void Begin()
        {
            _pending.Clear();
            _maxSpots = 0;
            Array.Clear(_needed, 0, _needed.Length);
        }

        /// <summary>Queues a visible tile of one of the crop's fields, <paramref name="near"/> to <paramref name="far"/> metres away.</summary>
        public void Add(RowCropJob job, Bounds bounds, float drawDistance, float near, float far)
        {
            _pending.Add(job);
            _maxSpots = Mathf.Max(_maxSpots, job.Spots);
            for (var lod = 0; lod < RowCropView.Lods; lod++)
            {
                if (!RowCropView.InBand(lod, _crop, drawDistance, near, far))
                {
                    continue;
                }

                if (_needed[lod] == 0)
                {
                    _bands[lod] = bounds;
                }

                _bands[lod].Encapsulate(bounds);
                _needed[lod] += _plantBound;
            }
        }

        /// <summary>Grows the queued tiles into the buffers, once per variant. RowCropKernelInputs binds the shared inputs first.</summary>
        public void Generate()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            UploadJobs();
            Reserve();
            var compute = _kernels.Compute;
            compute.SetBuffer(_kernels.Plant, CountersId, _counters);
            compute.SetBuffer(_kernels.Plant, CapacitiesId, _capacityBuffer);
            SetSowing(compute);
            for (var variant = 0; variant < _variants; variant++)
            {
                Dispatch(compute, variant);
            }

            compute.SetBuffer(_kernels.Count, CountersId, _counters);
            compute.SetBuffer(_kernels.Count, CapacitiesId, _capacityBuffer);
            compute.SetBuffer(_kernels.Count, ArgsId, _args);
            compute.SetInt(SlotsId, _meshes.Length);
            compute.Dispatch(_kernels.Count, 1, 1, 1);
        }

        /// <summary>Draws, for <paramref name="camera"/> only, each band that can hold plants, culled by its tiles' box.</summary>
        public void Draw(int layer, Camera camera)
        {
            for (var slot = 0; slot < _meshes.Length; slot++)
            {
                var lod = slot % RowCropView.Lods;
                if (_needed[lod] == 0)
                {
                    continue;
                }

                var card = lod == RowCropView.Card;
                _blocks[slot].SetFloat(CanopyHeightId, _crop.height);
                var parameters = new RenderParams(card ? _crop.cardMaterial : _crop.material)
                {
                    camera = camera,
                    worldBounds = _bands[lod],
                    layer = layer,
                    matProps = _blocks[slot],
                    receiveShadows = true,
                    shadowCastingMode = card && !_crop.cardShadows ? ShadowCastingMode.Off : ShadowCastingMode.On,
                    motionVectorMode = MotionVectorGenerationMode.Object,
                };
                Graphics.RenderMeshIndirect(parameters, _meshes[slot], _args, 1, slot);
            }
        }

        public void Dispose()
        {
            foreach (var buffer in _instances)
            {
                buffer?.Release();
            }

            _jobs?.Release();
            _args.Release();
            _counters.Release();
            _capacityBuffer.Release();
        }

        private void SetSowing(ComputeShader compute)
        {
            compute.SetVector(SowingId, new Vector4(_crop.rowSpacing, _crop.plantSpacing, _crop.jitter,
                _crop.yawJitter * Mathf.Deg2Rad));
            compute.SetFloat(FacesRowId, _crop.facesRow ? 1f : 0f);
            compute.SetVector(SizeId, new Vector4(_crop.scale.x, _crop.scale.y, _crop.lod0Distance, _crop.lod1Distance));
            compute.SetVector(VarietyId, new Vector4(_crop.lean * Mathf.Deg2Rad, _crop.edgeLean * Mathf.Deg2Rad,
                _crop.wander, 1f / _crop.wanderLength));
            compute.SetVector(PatchId, new Vector4(_crop.patchScale.x, _crop.patchScale.y, 1f / _crop.patchSize, 0f));
        }

        // Grows every slot's buffer to its share of the queued tiles before the kernel writes into it.
        private void Reserve()
        {
            for (var slot = 0; slot < _meshes.Length; slot++)
            {
                var share = Mathf.CeilToInt(_needed[slot % RowCropView.Lods] * VariantSlack / _variants);
                if (share > _instances[slot].count)
                {
                    Resize(slot, Mathf.Max(MinCapacity, share + share / 4));
                }
            }

            if (_resized)
            {
                _capacityBuffer.SetData(_capacities);
                _resized = false;
            }
        }

        private void Dispatch(ComputeShader compute, int variant)
        {
            var first = variant * RowCropView.Lods;
            for (var lod = 0; lod < RowCropView.Lods; lod++)
            {
                compute.SetBuffer(_kernels.Plant, OutputIds[lod], _instances[first + lod]);
            }

            compute.SetVector(VariantId, new Vector4(variant, _variants, first, 0f));
            var groups = Mathf.Max(1, (_maxSpots + ThreadGroup - 1) / ThreadGroup);
            compute.Dispatch(_kernels.Plant, groups, 1, _pending.Count);
        }

        private void UploadJobs()
        {
            if (_jobs == null || _jobs.count < _pending.Count)
            {
                _jobs?.Release();
                _jobs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(64, _pending.Count * 2), RowCropJob.Stride);
            }

            _jobs.SetData(_pending);
            _kernels.Compute.SetBuffer(_kernels.Plant, JobsId, _jobs);
        }

        private void Resize(int slot, int capacity)
        {
            _instances[slot]?.Release();
            _instances[slot] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, InstanceStride);
            _capacities[slot] = (uint)capacity;
            _blocks[slot].SetBuffer(InstancesId, _instances[slot]);
            _resized = true;
        }

        // Slot = variant * Lods + lod, the order of the buffers, counters and draw commands.
        private static Mesh[] Meshes(RowCrop crop)
        {
            var meshes = new Mesh[crop.variants.Count * RowCropView.Lods];
            for (var slot = 0; slot < meshes.Length; slot++)
            {
                meshes[slot] = crop.variants[slot / RowCropView.Lods].Mesh(slot % RowCropView.Lods);
            }

            return meshes;
        }

        private static GraphicsBuffer.IndirectDrawIndexedArgs[] Commands(Mesh[] meshes)
        {
            var commands = new GraphicsBuffer.IndirectDrawIndexedArgs[meshes.Length];
            for (var slot = 0; slot < meshes.Length; slot++)
            {
                commands[slot] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = meshes[slot].GetIndexCount(0),
                    startIndex = meshes[slot].GetIndexStart(0),
                    baseVertexIndex = meshes[slot].GetBaseVertex(0),
                };
            }

            return commands;
        }
    }
}
