using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Ground.Stamps;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>
    /// Grows a map's row crops on the GPU for each camera about to render, in edit mode too, and draws them per distance band.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.RowCropRenderer)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class RowCropRenderer : ScatterRenderer<RowCropBatch>
    {
        [Tooltip("The baked fields this map grows.")]
        public RowCropFields fields;

        private RowCropComputeInputs _inputs;

        private protected override ScatterSettings Settings
        {
            get
            {
                var quality = PresentationQuality.Current;
                return new ScatterSettings(quality.CropDensity, quality.CropDistance, quality.CropShadowDistance, 1f);
            }
        }

        private protected override ScatterComputeInputs Inputs => _inputs;

        // Crops bend under the trample map, which changes every frame it is published.
        private protected override bool RegrowsEveryFrame => TrampleSurfaces.TrampleGlobal.Published;

        private protected override void BuildInputs() => _inputs = new RowCropComputeInputs(compute, fields, terrain);

        private protected override ScatterField Field => fields;

        /// <summary>One batch per crop, in the field asset's crop order.</summary>
        private protected override List<RowCropBatch> NewBatches() => fields.crops.ConvertAll(crop => new RowCropBatch(crop, _inputs));

        private protected override void ReleaseInputs()
        {
            _inputs?.Dispose();
            _inputs = null;
        }

#if UNITY_EDITOR
        private protected override bool Stale(List<RowCropBatch> batches)
        {
            if (batches.Count != fields.crops.Count)
            {
                return true;
            }

            for (var i = 0; i < batches.Count; i++)
            {
                if (batches[i].Stale(fields.crops[i]))
                {
                    return true;
                }
            }

            return false;
        }
#endif

        /// <summary>Hands every visible tile to the batches of the crops growing on it.</summary>
        private protected override void Queue(List<RowCropBatch> batches, Vector3 position, ScatterSettings settings)
        {
            foreach (var t in Tiles.Visible)
            {
                var bounds = Tiles[t];
                var (near, far) = ScatterTiles.Distances(bounds, position);
                var tile = fields.tiles[t];
                var rect = fields.TileRect(tile);
                for (var f = tile.first; f < tile.first + tile.count; f++)
                {
                    var index = fields.tileFields[f];
                    var field = fields.fields[index];
                    var crop = fields.crops[field.crop];
                    var job = RowCropSpots.Job(rect, index, field, crop.rowSpacing, crop.plantSpacing);
                    batches[field.crop].Add(job, bounds, settings, near, far);
                }
            }
        }

        private protected override void AddProblems(List<string> problems)
        {
            if (fields == null)
            {
                problems.Add($"{nameof(fields)} is missing");
            }
            else
            {
                problems.AddRange(fields.Problems());
            }
        }
    }
}
