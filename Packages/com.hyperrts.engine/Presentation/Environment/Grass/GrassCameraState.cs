using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>One camera's grass batch and the view it was last scattered for, so a still camera only redraws.</summary>
    public sealed class GrassCameraState : IDisposable
    {
        private readonly ScatterView _view = new();

        internal GrassCameraState(GrassField field, GrassComputeInputs kernels, Material[] materials) =>
            Batch = new GrassBatch(field, kernels, materials);

        internal GrassBatch Batch { get; }

        /// <summary>Whether the batch already holds this view: same spot, settings and visible tiles.</summary>
        internal bool Holds(Vector3 position, GrassSettings settings, List<int> tiles) =>
            _view.Holds(position, settings.Packed, tiles);

        /// <summary>Records the view the batch was just scattered for.</summary>
        internal void Remember(Vector3 position, GrassSettings settings, List<int> tiles) =>
            _view.Remember(position, settings.Packed, tiles);

        public void Dispose() => Batch.Dispose();
    }
}
