using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Interaction;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>One camera's crop batches and the view they were last grown for, so a still camera only redraws.</summary>
    public sealed class RowCropCameraState : IDisposable
    {
        private readonly ScatterView _view = new();

        internal RowCropCameraState(RowCropFields fields, RowCropCompute kernels)
        {
            foreach (var crop in fields.crops)
            {
                Batches.Add(new RowCropBatch(crop, kernels));
            }
        }

        /// <summary>One batch per crop, in the field asset's crop order.</summary>
        internal List<RowCropBatch> Batches { get; } = new();

        /// <summary>Whether the batches already hold this view. Crops bend under the trample map, which changes every frame it is published.</summary>
        internal bool Holds(Vector3 position, ScatterSettings settings, List<int> tiles) =>
            !InteractionSurfaces.Published && _view.Holds(position, settings, tiles);

        /// <summary>Records the view the batches were just grown for.</summary>
        internal void Remember(Vector3 position, ScatterSettings settings, List<int> tiles) =>
            _view.Remember(position, settings, tiles);

        /// <summary>Whether the batches no longer match the field asset's crops or their models.</summary>
        internal bool Stale(RowCropFields fields)
        {
            if (Batches.Count != fields.crops.Count)
            {
                return true;
            }

            for (var i = 0; i < Batches.Count; i++)
            {
                if (Batches[i].Stale(fields.crops[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose() => Batches.ForEach(batch => batch.Dispose());
    }
}
