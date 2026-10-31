using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Grass
{
    /// <summary>One camera's grass batch and the view it was last scattered for, so a still camera only redraws.</summary>
    public sealed class GrassCameraState : IDisposable
    {
        // Grass reads the ash but not the trample map.
        private readonly ScatterView _view = new(readsTrample: false);

        internal GrassCameraState(GrassField field, GrassComputeInputs kernels, Material[] materials) =>
            Batch = new GrassBatch(field, kernels, materials);

        internal GrassBatch Batch { get; }

        /// <summary>Whether the batch already holds this view.</summary>
        internal bool Holds(Vector3 position, ScatterSettings settings, List<int> tiles) =>
            _view.Holds(position, settings, tiles);

        /// <summary>Records the view the batch was just scattered for.</summary>
        internal void Remember(Vector3 position, ScatterSettings settings, List<int> tiles) =>
            _view.Remember(position, settings, tiles);

        public void Dispose() => Batch.Dispose();
    }
}
