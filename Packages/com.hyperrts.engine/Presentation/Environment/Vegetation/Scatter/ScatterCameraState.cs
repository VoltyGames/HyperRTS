using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>One camera's scatter batches and the view they were last grown for, so a still camera only redraws.</summary>
    internal sealed class ScatterCameraState<TBatch> : IDisposable where TBatch : ScatterBatch
    {
        private readonly ScatterView _view = new();

        public ScatterCameraState(List<TBatch> batches) => Batches = batches;

        public List<TBatch> Batches { get; }

        public bool Holds(Vector3 position, ScatterSettings settings, List<int> tiles) =>
            _view.Holds(position, settings, tiles);

        /// <summary>Grows the jobs the batches were given since the last view, once the inputs are bound.</summary>
        public void Generate(Vector3 position, ScatterSettings settings, List<int> tiles)
        {
            foreach (var batch in Batches)
            {
                batch.Generate();
            }

            _view.Remember(position, settings, tiles);
        }

        public void Begin() => Batches.ForEach(batch => batch.Begin());

        public void Draw(int layer, Camera camera)
        {
            foreach (var batch in Batches)
            {
                batch.Draw(layer, camera);
            }
        }

        public void Dispose() => Batches.ForEach(batch => batch.Dispose());
    }
}
