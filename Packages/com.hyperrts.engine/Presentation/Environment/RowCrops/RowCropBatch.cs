using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>One crop's GPU side: visible tiles, the (variant, band) buffers RowCrops.compute fills, and the indirect draws.</summary>
    internal sealed class RowCropBatch : IDisposable
    {
        // Matches RowCropInstance in RowCrops.compute and RowCropInput.hlsl.
        private const int InstanceStride = 48;
        private const int ThreadGroup = 8;
        private const int MinCapacity = 1024;

        // Headroom over an even split, as the hash never splits plants between variants exactly evenly.
        private const float VariantSlack = 1.15f;

        private static readonly int JobsId = Shader.PropertyToID("_Jobs");
        private static readonly int CountersId = Shader.PropertyToID("_Counters");
        private static readonly int SowingId = Shader.PropertyToID("_Sowing");
        private static readonly int FacesRowId = Shader.PropertyToID("_FacesRow");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int VarietyId = Shader.PropertyToID("_Variety");
        private static readonly int PatchId = Shader.PropertyToID("_Patch");
        private static readonly int VariantId = Shader.PropertyToID("_Variant");
        private static readonly int CapacityId = Shader.PropertyToID("_Capacity");
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int[] OutputIds =
            { Shader.PropertyToID("_Full"), Shader.PropertyToID("_Simple"), Shader.PropertyToID("_Card") };

        private readonly RowCrop _crop;
        private readonly ComputeShader _compute;
        private readonly int _kernel;
        private readonly int _variants;
        private readonly Mesh[] _meshes;
        private readonly GraphicsBuffer[] _instances;
        private readonly MaterialPropertyBlock[] _blocks;
        private readonly uint[] _zeros;
        private readonly int[] _needed = new int[RowCropView.Lods];
        private readonly List<RowCropJob> _pending = new();
        private readonly GraphicsBuffer _args;
        private readonly GraphicsBuffer _counters;
        private readonly int _plantBound;
        private readonly Vector2Int _groups;
        private GraphicsBuffer _jobs;

        public RowCropBatch(RowCrop crop, ComputeShader compute, int kernel)
        {
            _crop = crop;
            _compute = compute;
            _kernel = kernel;
            _variants = crop.variants.Count;
            _meshes = Meshes(crop);
            _instances = new GraphicsBuffer[_meshes.Length];
            _blocks = new MaterialPropertyBlock[_meshes.Length];
            _zeros = new uint[_meshes.Length];
            _plantBound = RowCropLattice.PlantBound(RowCropFields.TileSize, crop.rowSpacing, crop.plantSpacing);
            var count = RowCropLattice.MaxCount(RowCropFields.TileSize, crop.rowSpacing, crop.plantSpacing);
            _groups = new Vector2Int(Groups(count.x), Groups(count.y));
            _counters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _meshes.Length, sizeof(uint));
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

        /// <summary>Forgets the last camera's tiles.</summary>
        public void Begin()
        {
            _pending.Clear();
            Array.Clear(_needed, 0, _needed.Length);
        }

        /// <summary>Queues a visible tile of one of the crop's fields, <paramref name="near"/> to <paramref name="far"/> metres away.</summary>
        public void Add(RowCropJob job, float drawDistance, float near, float far)
        {
            _pending.Add(job);
            for (var lod = 0; lod < RowCropView.Lods; lod++)
            {
                if (RowCropView.InBand(lod, _crop, drawDistance, near, far))
                {
                    _needed[lod] += _plantBound;
                }
            }
        }

        /// <summary>Grows this camera's plants into the buffers, once per variant. The renderer binds the shared inputs first.</summary>
        public void Generate()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            UploadJobs();
            _counters.SetData(_zeros);
            _compute.SetBuffer(_kernel, CountersId, _counters);
            SetSowing();
            for (var variant = 0; variant < _variants; variant++)
            {
                Dispatch(variant);
            }

            for (var slot = 0; slot < _meshes.Length; slot++)
            {
                GraphicsBuffer.CopyCount(_instances[slot], _args, slot * GraphicsBuffer.IndirectDrawIndexedArgs.size + sizeof(uint));
            }
        }

        /// <summary>Draws, for <paramref name="camera"/> only, each band that can hold plants.</summary>
        public void Draw(Bounds worldBounds, int layer, Camera camera)
        {
            for (var slot = 0; slot < _meshes.Length; slot++)
            {
                var lod = slot % RowCropView.Lods;
                if (_needed[lod] == 0)
                {
                    continue;
                }

                var card = lod == RowCropView.Card;
                var parameters = new RenderParams(card ? _crop.cardMaterial : _crop.material)
                {
                    camera = camera,
                    worldBounds = worldBounds,
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
        }

        private void SetSowing()
        {
            _compute.SetVector(SowingId, new Vector4(_crop.rowSpacing, _crop.plantSpacing, _crop.jitter,
                _crop.yawJitter * Mathf.Deg2Rad));
            _compute.SetFloat(FacesRowId, _crop.facesRow ? 1f : 0f);
            _compute.SetVector(SizeId, new Vector4(_crop.scale.x, _crop.scale.y, _crop.lod0Distance, _crop.lod1Distance));
            _compute.SetVector(VarietyId, new Vector4(_crop.lean * Mathf.Deg2Rad, _crop.edgeLean * Mathf.Deg2Rad,
                _crop.wander, 1f / _crop.wanderLength));
            _compute.SetVector(PatchId, new Vector4(_crop.patchScale.x, _crop.patchScale.y, 1f / _crop.patchSize, 0f));
        }

        private void Dispatch(int variant)
        {
            var first = variant * RowCropView.Lods;
            for (var lod = 0; lod < RowCropView.Lods; lod++)
            {
                var slot = first + lod;
                var share = Mathf.CeilToInt(_needed[lod] * VariantSlack / _variants);
                if (share > _instances[slot].count)
                {
                    Resize(slot, Mathf.Max(MinCapacity, share + share / 4));
                }

                _instances[slot].SetCounterValue(0);
                _compute.SetBuffer(_kernel, OutputIds[lod], _instances[slot]);
            }

            _compute.SetVector(CapacityId, new Vector4(_instances[first].count, _instances[first + 1].count,
                _instances[first + 2].count, 0f));
            _compute.SetVector(VariantId, new Vector4(variant, _variants, first, 0f));
            _compute.Dispatch(_kernel, _groups.x, _groups.y, _pending.Count);
        }

        private void UploadJobs()
        {
            if (_jobs == null || _jobs.count < _pending.Count)
            {
                _jobs?.Release();
                _jobs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(64, _pending.Count * 2), RowCropJob.Stride);
            }

            _jobs.SetData(_pending);
            _compute.SetBuffer(_kernel, JobsId, _jobs);
        }

        private void Resize(int slot, int capacity)
        {
            _instances[slot]?.Release();
            _instances[slot] = new GraphicsBuffer(GraphicsBuffer.Target.Append, capacity, InstanceStride);
            _blocks[slot].SetBuffer(InstancesId, _instances[slot]);
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

        private static int Groups(int threads) => (threads + ThreadGroup - 1) / ThreadGroup;
    }
}
