using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>One camera's grass batch and the view it was last scattered for, so a still camera only redraws.</summary>
    internal sealed class GrassCameraState : IDisposable
    {
        private readonly ScatterView _view = new();

        public GrassCameraState(GrassField field, GrassComputeInputs kernels, Material[] materials) =>
            Batch = new GrassBatch(field, kernels, materials);

        public GrassBatch Batch { get; }

        /// <summary>Whether the batch already holds this view: same spot, settings and visible tiles.</summary>
        public bool Holds(Vector3 position, GrassSettings settings, List<int> tiles) =>
            _view.Holds(position, settings.Packed, tiles);

        /// <summary>Records the view the batch was just scattered for.</summary>
        public void Remember(Vector3 position, GrassSettings settings, List<int> tiles) =>
            _view.Remember(position, settings.Packed, tiles);

        public void Dispose() => Batch.Dispose();
    }
}
