using System;
using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Heights
{
    /// <summary>
    /// Drainage from 0 on ridges to 1 in a swale, from each cell's upslope catchment (D8), averaged over the symmetry
    /// copies.
    /// </summary>
    public sealed class MapFlow
    {
        private const float CellSize = 2f;

        // Catchment (m²) where drainage starts to show and shows fully.
        private const float RillArea = 400f;
        private const float SwaleArea = 20000f;

        private static readonly (int X, int Z)[] Neighbours =
            { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

        private readonly MapSymmetry _symmetry;
        private readonly Rect _bounds;
        private readonly int _width;
        private readonly int _height;
        private readonly float[] _strength;

        public MapFlow(MapSymmetry symmetry, MapHeights heights)
        {
            _symmetry = symmetry;
            _bounds = heights.Bounds;
            _width = Mathf.Max(2, Mathf.CeilToInt(_bounds.width / CellSize));
            _height = Mathf.Max(2, Mathf.CeilToInt(_bounds.height / CellSize));
            var ground = new float[_width * _height];
            for (var i = 0; i < ground.Length; i++)
            {
                ground[i] = heights.Height(Center(i % _width, i / _width));
            }

            _strength = Catchment(ground).Select(Strength).ToArray();
        }

        public float At(Vector2 point)
        {
            var sector = _symmetry.ToSector(point, out _);
            var sum = 0f;
            foreach (var copy in _symmetry.Copies)
            {
                sum += Sample(copy.Apply(sector));
            }

            return sum / _symmetry.Copies.Count;
        }

        /// <summary>Square metres draining through each cell.</summary>
        private float[] Catchment(float[] ground)
        {
            var catchment = Enumerable.Repeat(CellSize * CellSize, ground.Length).ToArray();
            foreach (var cell in Enumerable.Range(0, ground.Length).OrderByDescending(i => ground[i]))
            {
                var lower = Steepest(ground, cell);
                if (lower >= 0)
                {
                    catchment[lower] += catchment[cell];
                }
            }

            return catchment;
        }

        /// <summary>The neighbour a cell drains to, or -1.</summary>
        private int Steepest(float[] ground, int cell)
        {
            var (x, z) = (cell % _width, cell / _width);
            var (best, drop) = (-1, 0f);
            foreach (var (dx, dz) in Neighbours)
            {
                var (nx, nz) = (x + dx, z + dz);
                if (nx < 0 || nz < 0 || nx >= _width || nz >= _height)
                {
                    continue;
                }

                var next = nz * _width + nx;
                var slope = (ground[cell] - ground[next]) / (dx != 0 && dz != 0 ? 1.41421356f : 1f);
                if (slope > drop)
                {
                    (best, drop) = (next, slope);
                }
            }

            return best;
        }

        private static float Strength(float catchment)
        {
            var t = (Mathf.Log(catchment) - Mathf.Log(RillArea)) / (Mathf.Log(SwaleArea) - Mathf.Log(RillArea));
            return MapGeometry.Smooth(t);
        }

        private Vector2 Center(int x, int z) => _bounds.min + new Vector2(x + 0.5f, z + 0.5f) * CellSize;

        private float Sample(Vector2 point)
        {
            var at = (point - _bounds.min) / CellSize - new Vector2(0.5f, 0.5f);
            var x0 = Math.Clamp(Mathf.FloorToInt(at.x), 0, _width - 2);
            var z0 = Math.Clamp(Mathf.FloorToInt(at.y), 0, _height - 2);
            var (fx, fz) = (Mathf.Clamp01(at.x - x0), Mathf.Clamp01(at.y - z0));
            var row = z0 * _width + x0;
            var low = Mathf.Lerp(_strength[row], _strength[row + 1], fx);
            var high = Mathf.Lerp(_strength[row + _width], _strength[row + _width + 1], fx);
            return Mathf.Lerp(low, high, fz);
        }
    }
}
