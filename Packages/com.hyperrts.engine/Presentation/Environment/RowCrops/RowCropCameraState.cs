using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>One camera's crop batches and the view they were last grown for, so a still camera only redraws.</summary>
    internal sealed class RowCropCameraState : IDisposable
    {
        // Metres the camera may drift before its plants regrow; fades and bands lag by at most this.
        private const float Step = 0.5f;

        private readonly List<int> _tiles = new();
        private Vector3Int _cell;
        private float _density;
        private float _distance;
        private bool _grown;

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
        public bool Holds(Vector3 position, float density, float distance, List<int> tiles)
        {
            if (!_grown || RowCropKernelInputs.Trampled)
            {
                return false;
            }

            var settings = density == _density && distance == _distance;
            return settings && Cell(position) == _cell && SameTiles(tiles);
        }

        /// <summary>Records the view the batches were just grown for.</summary>
        public void Remember(Vector3 position, float density, float distance, List<int> tiles)
        {
            _cell = Cell(position);
            _density = density;
            _distance = distance;
            _tiles.Clear();
            _tiles.AddRange(tiles);
            _grown = true;
        }

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

        private bool SameTiles(List<int> tiles)
        {
            if (tiles.Count != _tiles.Count)
            {
                return false;
            }

            for (var i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] != _tiles[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3Int Cell(Vector3 position) => Vector3Int.RoundToInt(position / Step);
    }
}
