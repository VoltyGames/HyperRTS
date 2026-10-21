using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>One camera's crop batches and the view they were last grown for, so a still camera only redraws.</summary>
    internal sealed class RowCropCameraState : IDisposable
    {
        private readonly ScatterView _view = new();

        public RowCropCameraState(RowCropFields fields, RowCropKernels kernels)
        {
            foreach (var crop in fields.crops)
            {
                Batches.Add(new RowCropBatch(crop, kernels));
            }
        }

        /// <summary>One batch per crop, in the field asset's crop order.</summary>
        public List<RowCropBatch> Batches { get; } = new();

        /// <summary>Whether the batches already hold this view: same spot, settings and visible tiles, and no trample map.</summary>
        public bool Holds(Vector3 position, float density, float distance, List<int> tiles) =>
            !RowCropKernelInputs.Trampled && _view.Holds(position, new Vector4(density, distance), tiles);

        /// <summary>Records the view the batches were just grown for.</summary>
        public void Remember(Vector3 position, float density, float distance, List<int> tiles) =>
            _view.Remember(position, new Vector4(density, distance), tiles);

        /// <summary>Whether the batches no longer match the field asset's crops or their models.</summary>
        public bool Stale(RowCropFields fields)
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
