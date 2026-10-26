using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>One crop's GPU side for one camera: visible tiles, the (variant, band) buffers RowCrops.compute fills, and the indirect draws.</summary>
    internal sealed class RowCropBatch : ScatterBatch<RowCropJob>
    {
        private const int MinCapacity = 1024;

        // Headroom over an even split, as the hash never splits plants between variants exactly evenly.
        private const float VariantSlack = 1.15f;

        private static readonly int CapacitiesId = Shader.PropertyToID("_Capacities");
        private static readonly int SlotsId = Shader.PropertyToID("_Slots");
        private static readonly int PlantingId = Shader.PropertyToID("_Planting");
        private static readonly int FacesRowId = Shader.PropertyToID("_FacesRow");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int VarietyId = Shader.PropertyToID("_Variety");
        private static readonly int GrowthId = Shader.PropertyToID("_Growth");
        private static readonly int VariantId = Shader.PropertyToID("_Variant");
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int CanopyHeightId = Shader.PropertyToID("_CanopyHeight");
        private static readonly int[] OutputIds =
            { Shader.PropertyToID("_Full"), Shader.PropertyToID("_Simple"), Shader.PropertyToID("_Card") };

        private readonly RowCrop _crop;
        private readonly int _variants;
        private readonly GraphicsBuffer[] _instances;
        private readonly uint[] _capacities;
        private readonly int[] _needed = new int[RowCropView.Lods];
        private readonly Bounds[] _bands = new Bounds[RowCropView.Lods];
        private readonly GraphicsBuffer _capacityBuffer;
        private readonly int _plantBound;
        private bool _resized;

        public RowCropBatch(RowCrop crop, RowCropCompute kernels)
            : base(kernels.Compute, kernels.Plant, kernels.Count, SlotMeshes(crop), RowCropJob.Stride)
        {
            _crop = crop;
            _variants = crop.variants.Count;
            _instances = new GraphicsBuffer[Meshes.Length];
            _capacities = new uint[Meshes.Length];
            _plantBound = RowCropSpots.PlantBound(RowCropFields.TileSize, crop.rowSpacing, crop.plantSpacing);
            _capacityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Meshes.Length, sizeof(uint));
            for (var slot = 0; slot < Meshes.Length; slot++)
            {
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

            for (var slot = 0; slot < Meshes.Length; slot++)
            {
                var variant = crop.variants[slot / RowCropView.Lods];
                var mesh = variant?.Mesh(slot % RowCropView.Lods);
                if (mesh == null || mesh != Meshes[slot])
                {
                    return true;
                }
            }

            return false;
        }

        protected override void ForgetView() => Array.Clear(_needed, 0, _needed.Length);

        /// <summary>Queues a visible tile of one of the crop's fields, <paramref name="near"/> to <paramref name="far"/> metres away.</summary>
        public void Add(RowCropJob job, Bounds bounds, float drawDistance, float near, float far)
        {
            Queue(job, job.Spots);
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

        /// <summary>Grows the queued tiles once per variant.</summary>
        protected override void Grow()
        {
            Compute.SetBuffer(GrowKernel, CapacitiesId, _capacityBuffer);
            SetPlanting(Compute);
            for (var variant = 0; variant < _variants; variant++)
            {
                Dispatch(Compute, variant);
            }
        }

        protected override void BindCapacities()
        {
            Compute.SetBuffer(CountKernel, CapacitiesId, _capacityBuffer);
            Compute.SetInt(SlotsId, Meshes.Length);
        }

        protected override bool HasRoom(int slot) => _needed[slot % RowCropView.Lods] != 0;

        protected override RenderParams Parameters(int slot)
        {
            var card = slot % RowCropView.Lods == RowCropView.Card;
            Blocks[slot].SetFloat(CanopyHeightId, _crop.height);
            return new RenderParams(card ? _crop.cardMaterial : _crop.material)
            {
                worldBounds = _bands[slot % RowCropView.Lods],
                shadowCastingMode = card && !_crop.cardShadows ? ShadowCastingMode.Off : ShadowCastingMode.On,
            };
        }

        protected override void ReleaseBuffers()
        {
            foreach (var buffer in _instances)
            {
                buffer?.Release();
            }

            _capacityBuffer.Release();
        }

        private void SetPlanting(ComputeShader compute)
        {
            compute.SetVector(PlantingId, new Vector4(_crop.rowSpacing, _crop.plantSpacing, _crop.jitter,
                _crop.yawJitter * Mathf.Deg2Rad));
            compute.SetFloat(FacesRowId, _crop.facesRow ? 1f : 0f);
            compute.SetVector(SizeId, new Vector4(_crop.scale.x, _crop.scale.y, _crop.lod0Distance, _crop.lod1Distance));
            compute.SetVector(VarietyId, new Vector4(_crop.lean * Mathf.Deg2Rad, _crop.edgeLean * Mathf.Deg2Rad,
                _crop.wander, 1f / _crop.wanderLength));
            var growth = _crop.growthVariation;
            compute.SetVector(GrowthId, new Vector4(growth.x, growth.y, 1f / _crop.growthVariationSize, 0f));
        }

        // Grows every slot's buffer to its share of the queued tiles.
        protected override void Reserve()
        {
            for (var slot = 0; slot < Meshes.Length; slot++)
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
                compute.SetBuffer(GrowKernel, OutputIds[lod], _instances[first + lod]);
            }

            compute.SetVector(VariantId, new Vector4(variant, _variants, first, 0f));
            DispatchGrow();
        }

        private void Resize(int slot, int capacity)
        {
            _instances[slot]?.Release();
            _instances[slot] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, InstanceStride);
            _capacities[slot] = (uint)capacity;
            Blocks[slot].SetBuffer(InstancesId, _instances[slot]);
            _resized = true;
        }

        // Slot = variant * Lods + lod, the order of the buffers, counters and draw commands.
        private static Mesh[] SlotMeshes(RowCrop crop)
        {
            var meshes = new Mesh[crop.variants.Count * RowCropView.Lods];
            for (var slot = 0; slot < meshes.Length; slot++)
            {
                meshes[slot] = crop.variants[slot / RowCropView.Lods].Mesh(slot % RowCropView.Lods);
            }

            return meshes;
        }
    }
}
