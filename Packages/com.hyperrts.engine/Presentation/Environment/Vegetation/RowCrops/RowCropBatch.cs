using System;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>One crop's GPU side for one camera: visible tiles, a slot per (variant, band) that RowCrops.compute fills, and the indirect draws.</summary>
    public sealed class RowCropBatch : ScatterBatch
    {
        private const int MinCapacity = 1024;

        // Headroom over an even split, as the hash never splits plants between variants exactly evenly.
        private const float VariantSlack = 1.15f;

        private static readonly int PlantingId = Shader.PropertyToID("_Planting");
        private static readonly int FacesRowId = Shader.PropertyToID("_FacesRow");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int VarietyId = Shader.PropertyToID("_Variety");
        private static readonly int GrowthId = Shader.PropertyToID("_Growth");
        private static readonly int StandId = Shader.PropertyToID("_Stand");
        private static readonly int VariantsId = Shader.PropertyToID("_Variants");
        private static readonly int CanopyHeightId = Shader.PropertyToID("_CanopyHeight");

        private readonly RowCrop _crop;
        private readonly int _variants;
        private readonly uint[] _capacities;
        private readonly int[] _needed = new int[RowCropBands.Lods];
        private readonly Bounds[] _bands = new Bounds[RowCropBands.Lods];
        private readonly int _plantBound;
        private readonly float _height;

        internal RowCropBatch(RowCrop crop, RowCropComputeInputs kernels)
            : base(kernels, SlotMeshes(crop), MinCapacity)
        {
            _crop = crop;
            _variants = crop.variants.Count;
            _capacities = new uint[Meshes.Length];
            _plantBound = RowCropSpots.PlantBound(ScatterTiles.Size, crop.rowSpacing, crop.plantSpacing);
            _height = crop.height;
            foreach (var block in Blocks)
            {
                block.SetFloat(CanopyHeightId, _height);
            }
        }

        /// <summary>Whether the batch no longer draws <paramref name="crop"/> as it is: another crop, its height or its models changed.</summary>
        internal bool Stale(RowCrop crop)
        {
            if (crop == null || crop != _crop || crop.variants.Count != _variants || crop.height != _height)
            {
                return true;
            }

            for (var slot = 0; slot < Meshes.Length; slot++)
            {
                var variant = crop.variants[slot / RowCropBands.Lods];
                var mesh = variant?.Mesh(slot % RowCropBands.Lods);
                if (mesh == null || mesh != Meshes[slot])
                {
                    return true;
                }
            }

            return false;
        }

        private protected override void ForgetView() => Array.Clear(_needed, 0, _needed.Length);

        /// <summary>Queues a visible tile of one of the crop's fields, <paramref name="near"/> to <paramref name="far"/> metres away.</summary>
        internal void Add(ScatterJob job, Bounds bounds, float drawDistance, float near, float far)
        {
            Queue(job);
            for (var lod = 0; lod < RowCropBands.Lods; lod++)
            {
                if (!RowCropBands.InBand(lod, _crop, drawDistance, near, far))
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

        private protected override void BindShaping()
        {
            Compute.SetVector(PlantingId, new Vector4(_crop.rowSpacing, _crop.plantSpacing, _crop.jitter,
                _crop.yawJitter * Mathf.Deg2Rad));
            Compute.SetFloat(FacesRowId, _crop.facesRow ? 1f : 0f);
            Compute.SetVector(SizeId, new Vector4(_crop.scale.x, _crop.scale.y, _crop.lod0Distance, _crop.lod1Distance));
            Compute.SetVector(VarietyId, new Vector4(_crop.lean * Mathf.Deg2Rad, _crop.edgeLean * Mathf.Deg2Rad,
                _crop.wander, 1f / _crop.wanderLength));
            var growth = _crop.growthVariation;
            Compute.SetVector(GrowthId, new Vector4(growth.x, growth.y, 1f / _crop.growthVariationSize, _crop.edgeGrowth));
            Compute.SetFloat(StandId, _crop.stand);
            Compute.SetInt(VariantsId, _variants);
        }

        // Each slot holds its band's share of the queued tiles' worst case.
        private protected override void Reserve()
        {
            for (var slot = 0; slot < Meshes.Length; slot++)
            {
                var needed = _needed[slot % RowCropBands.Lods];
                var share = Mathf.CeilToInt(needed * VariantSlack / _variants);
                _capacities[slot] = needed == 0 ? 0u : (uint)Mathf.Max(MinCapacity, share + share / 4);
            }

            Slots.Fit(_capacities);
        }

        private protected override RenderParams Parameters(int slot)
        {
            var card = slot % RowCropBands.Lods == RowCropBands.Card;
            return new RenderParams(card ? _crop.cardMaterial : _crop.material)
            {
                worldBounds = _bands[slot % RowCropBands.Lods],
                shadowCastingMode = card && !_crop.cardShadows ? ShadowCastingMode.Off : ShadowCastingMode.On,
            };
        }

        // Slot = variant * Lods + lod, the order of the buffers, counters and draw commands.
        private static Mesh[] SlotMeshes(RowCrop crop)
        {
            var meshes = new Mesh[crop.variants.Count * RowCropBands.Lods];
            for (var slot = 0; slot < meshes.Length; slot++)
            {
                meshes[slot] = crop.variants[slot / RowCropBands.Lods].Mesh(slot % RowCropBands.Lods);
            }

            return meshes;
        }
    }
}
