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
        private readonly MapField<float> _strength;

        public MapFlow(MapSymmetry symmetry, MapHeights heights)
        {
            _symmetry = symmetry;
            _strength = new MapField<float>(MapGrid.Centres(heights.Bounds, CellSize));
            var ground = new float[_strength.Grid.Count];
            for (var i = 0; i < ground.Length; i++)
            {
                ground[i] = heights.Height(_strength.Grid.Position(i));
            }

            var catchment = Catchment(ground);
            for (var i = 0; i < ground.Length; i++)
            {
                _strength[i] = Strength(catchment[i]);
            }
        }

        public float At(Vector2 point)
        {
            var sector = _symmetry.ToSector(point, out _);
            var sum = 0f;
            foreach (var copy in _symmetry.Copies)
            {
                sum += _strength.Grid.Bilinear(copy.Apply(sector)).Blend(_strength);
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
            var grid = _strength.Grid;
            var at = grid.Frame.Cell(cell);
            var (best, drop) = (-1, 0f);
            foreach (var (dx, dz) in Neighbours)
            {
                var (nx, nz) = (at.x + dx, at.y + dz);
                if (!grid.Contains(nx, nz))
                {
                    continue;
                }

                var next = grid.Index(nx, nz);
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
    }
}
