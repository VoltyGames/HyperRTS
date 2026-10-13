using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// One crop's GPU side: the visible tiles of its fields this frame, an append buffer per distance band that
    /// RowCrops.compute fills with plants, and the indirect draws that read them back through HyperRTS/RowCrop.
    /// Buffers grow to the bands in view and are kept between frames.
    /// </summary>
    internal sealed class RowCropBatch : IDisposable
    {
        // RowCropInstance in RowCrops.compute and RowCropInput.hlsl.
        private const int InstanceStride = 48;
        private const int ThreadGroup = 8;
        private const int MinCapacity = 1024;

        private static readonly int JobsId = Shader.PropertyToID("_Jobs");
        private static readonly int CountersId = Shader.PropertyToID("_Counters");
        private static readonly int SowingId = Shader.PropertyToID("_Sowing");
        private static readonly int FacesRowId = Shader.PropertyToID("_FacesRow");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int CapacityId = Shader.PropertyToID("_Capacity");
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int[] OutputIds =
            { Shader.PropertyToID("_Full"), Shader.PropertyToID("_Simple"), Shader.PropertyToID("_Card") };

        private static readonly uint[] Zeros = new uint[RowCropView.Lods];

        private readonly RowCrop _crop;
        private readonly ComputeShader _compute;
        private readonly int _kernel;
        private readonly Mesh[] _meshes;
        private readonly GraphicsBuffer[] _instances = new GraphicsBuffer[RowCropView.Lods];
        private readonly MaterialPropertyBlock[] _blocks = new MaterialPropertyBlock[RowCropView.Lods];
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
            _meshes = new[] { crop.lod0, crop.lod1, crop.card };
            _plantBound = RowCropLattice.PlantBound(RowCropFields.TileSize, crop.rowSpacing, crop.plantSpacing);
            var count = RowCropLattice.MaxCount(RowCropFields.TileSize, crop.rowSpacing, crop.plantSpacing);
            _groups = new Vector2Int(Groups(count.x), Groups(count.y));
            _counters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, RowCropView.Lods, sizeof(uint));
            _args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, RowCropView.Lods,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _args.SetData(Commands(_meshes));
            for (var lod = 0; lod < RowCropView.Lods; lod++)
            {
                _blocks[lod] = new MaterialPropertyBlock();
                Resize(lod, MinCapacity);
            }
        }

        /// <summary>Forgets last frame's tiles.</summary>
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

        /// <summary>Grows this frame's plants into the band buffers; the renderer has set the shared kernel inputs.</summary>
        public void Generate()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            for (var lod = 0; lod < RowCropView.Lods; lod++)
            {
                if (_needed[lod] > _instances[lod].count)
                {
                    Resize(lod, Mathf.Max(MinCapacity, _needed[lod] + _needed[lod] / 4));
                }

                _instances[lod].SetCounterValue(0);
                _compute.SetBuffer(_kernel, OutputIds[lod], _instances[lod]);
            }

            UploadJobs();
            _counters.SetData(Zeros);
            _compute.SetBuffer(_kernel, CountersId, _counters);
            _compute.SetVector(SowingId, new Vector4(_crop.rowSpacing, _crop.plantSpacing, _crop.jitter,
                _crop.yawJitter * Mathf.Deg2Rad));
            _compute.SetFloat(FacesRowId, _crop.facesRow ? 1f : 0f);
            _compute.SetVector(SizeId, new Vector4(_crop.scale.x, _crop.scale.y, _crop.lod0Distance, _crop.lod1Distance));
            _compute.SetVector(CapacityId, new Vector4(_instances[0].count, _instances[1].count, _instances[2].count, 0f));
            _compute.Dispatch(_kernel, _groups.x, _groups.y, _pending.Count);
            for (var lod = 0; lod < RowCropView.Lods; lod++)
            {
                GraphicsBuffer.CopyCount(_instances[lod], _args, lod * GraphicsBuffer.IndirectDrawIndexedArgs.size + sizeof(uint));
            }
        }

        /// <summary>Draws each band that can hold plants this frame.</summary>
        public void Draw(Bounds worldBounds, int layer)
        {
            for (var lod = 0; lod < RowCropView.Lods; lod++)
            {
                if (_needed[lod] == 0)
                {
                    continue;
                }

                var card = lod == RowCropView.Card;
                var parameters = new RenderParams(card ? _crop.cardMaterial : _crop.material)
                {
                    worldBounds = worldBounds,
                    layer = layer,
                    matProps = _blocks[lod],
                    receiveShadows = true,
                    shadowCastingMode = card && !_crop.cardShadows ? ShadowCastingMode.Off : ShadowCastingMode.On,
                    motionVectorMode = MotionVectorGenerationMode.Object,
                };
                Graphics.RenderMeshIndirect(parameters, _meshes[lod], _args, 1, lod);
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

        private void Resize(int lod, int capacity)
        {
            _instances[lod]?.Release();
            _instances[lod] = new GraphicsBuffer(GraphicsBuffer.Target.Append, capacity, InstanceStride);
            _blocks[lod].SetBuffer(InstancesId, _instances[lod]);
        }

        private static GraphicsBuffer.IndirectDrawIndexedArgs[] Commands(Mesh[] meshes)
        {
            var commands = new GraphicsBuffer.IndirectDrawIndexedArgs[meshes.Length];
            for (var lod = 0; lod < meshes.Length; lod++)
            {
                commands[lod] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = meshes[lod].GetIndexCount(0),
                    startIndex = meshes[lod].GetIndexStart(0),
                    baseVertexIndex = meshes[lod].GetBaseVertex(0),
                };
            }

            return commands;
        }

        private static int Groups(int threads) => (threads + ThreadGroup - 1) / ThreadGroup;
    }
}
