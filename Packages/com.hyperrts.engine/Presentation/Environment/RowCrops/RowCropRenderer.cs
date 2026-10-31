using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Quality;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// Grows a map's row crops on the GPU from <see cref="RowCropFields"/> for each camera about to render, in edit
    /// mode too, and draws them per distance band. Each camera keeps its own buffers and regrows only when its view,
    /// the quality settings or the trample map change.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class RowCropRenderer : ScatterRenderer<RowCropCameraState>
    {
        [Tooltip("The baked fields this map grows.")]
        public RowCropFields fields;

        private RowCropCompute _kernels;
        private RowCropComputeInputs _inputs;

        private protected override float DrawDistance => PresentationQuality.Current.CropDistance;

        private protected override void BuildInputs()
        {
            _kernels = new RowCropCompute(compute);
            _inputs = new RowCropComputeInputs(_kernels, fields, terrain);
        }

        private protected override ScatterTiles NewTiles() =>
            new(fields.tiles.Count, t => fields.TileRect(fields.tiles[t]), fields.TallestPlant, terrain);

        private protected override RowCropCameraState NewState() => new(fields, _kernels);

        private protected override void ReleaseInputs()
        {
            _inputs?.Dispose();
            _inputs = null;
        }

        private protected override void Draw(RowCropCameraState state, Camera camera, Vector3 position)
        {
            var quality = PresentationQuality.Current;
            if (!state.Holds(position, quality.CropDensity, quality.CropDistance, Tiles.Visible))
            {
                Grow(state, position);
            }

            foreach (var batch in state.Batches)
            {
                batch.Draw(gameObject.layer, camera);
            }
        }

        private void Grow(RowCropCameraState state, Vector3 position)
        {
            var quality = PresentationQuality.Current;
            Queue(state.Batches, position, quality.CropDistance);
            _inputs.SetCamera(position, quality.CropDensity, quality.CropDistance);
            foreach (var batch in state.Batches)
            {
                batch.Generate();
            }

            state.Remember(position, quality.CropDensity, quality.CropDistance, Tiles.Visible);
        }

#if UNITY_EDITOR
        // Catches a map rebuild that reshaped the fields or the crops while the scene stayed open.
        private protected override bool InputsStale()
        {
            if (fields == null)
            {
                return true;
            }

            return _inputs.Stale || Tiles.Count != fields.tiles.Count;
        }

        private protected override bool Stale(RowCropCameraState state) => state.Stale(fields);
#endif

        /// <summary>Hands every visible tile to the batches of the crops growing on it.</summary>
        private void Queue(List<RowCropBatch> batches, Vector3 position, float distance)
        {
            batches.ForEach(batch => batch.Begin());
            foreach (var t in Tiles.Visible)
            {
                var bounds = Tiles[t];
                var (near, far) = ScatterTiles.Distances(bounds, position);
                var tile = fields.tiles[t];
                var rect = fields.TileRect(tile);
                for (var f = tile.firstField; f < tile.firstField + tile.fieldCount; f++)
                {
                    var index = fields.tileFields[f];
                    var field = fields.fields[index];
                    var crop = fields.crops[field.crop];
                    var job = new RowCropJob(rect, index, field, crop.rowSpacing, crop.plantSpacing);
                    batches[field.crop].Add(job, bounds, distance, near, far);
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
