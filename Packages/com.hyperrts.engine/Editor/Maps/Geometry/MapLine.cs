using HyperRTS.Simulation.Common.Cells;
using System.Collections.Generic;
using HyperRTS.Simulation.Common.Geometry;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>
    /// A placed polyline (river, road, ravine) in map space with its width, a bounds box for quick rejects and its
    /// segments sorted into grid cells, so a distance lookup only tests the segments near the point.
    /// </summary>
    public sealed class MapLine
    {
        private const int MaxCellsAcross = 128;
        private const float MinCellSize = 0.5f;

        // Past half a cell's diagonal (0.707), so rounding never drops a segment from a cell it crosses.
        private const float CellReach = 0.75f;

        private readonly float[] _walked;
        private readonly float[] _lengths;
        private readonly CellGrid _grid;
        private readonly int[] _cellStarts;
        private readonly int[] _cellSegments;

        public MapLine(List<Vector2> points, float width, float reach)
        {
            Points = points;
            Width = width;
            Bounds = MapGeometry.Bounds(points, reach);
            Path = new MapLineWalker(points);
            var segments = math.max(points.Count - 1, 0);
            _walked = new float[segments];
            _lengths = new float[segments];
            Length = Measure();
            _grid = CreateGrid(segments);
            (_cellStarts, _cellSegments) = SortIntoCells(segments);
        }

        public List<Vector2> Points { get; }

        public float Width { get; }

        public float Length { get; }

        /// <summary>The line measured once for repeated lookups of points along it.</summary>
        public MapLineWalker Path { get; }

        /// <summary>The points' box grown by the line's reach: nothing outside it is affected by the line.</summary>
        public Rect Bounds { get; }

        /// <summary>Distance to the line, or infinity outside its reach.</summary>
        public float Distance(Vector2 point) => Distance(point, out _);

        /// <summary>
        /// Distance to the line and the closest point's distance along it, as <see
        /// cref="MapGeometry.DistanceToPolyline(IReadOnlyList{Vector2}, Vector2, out float)"/> finds them, or
        /// infinity outside the line's reach.
        /// </summary>
        public float Distance(Vector2 point, out float along)
        {
            along = 0f;
            if (!Bounds.Contains(point))
            {
                return float.PositiveInfinity;
            }

            var closest = new Closest { Distance = float.MaxValue, Segment = -1 };
            var center = _grid.Cell((float2)point);
            var rings = math.cmax(math.max(center, _grid.Size - 1 - center));
            for (var ring = 0; ring <= rings; ring++)
            {
                SearchRing(point, center, ring, ref closest);

                // Segments not yet tested lie at least a ring away; the spare ring absorbs rounding, so ties stay found.
                if (closest.Distance < (ring - 1) * _grid.CellSize)
                {
                    break;
                }
            }

            if (closest.Segment >= 0)
            {
                along = _walked[closest.Segment] + closest.T * _lengths[closest.Segment];
            }

            return closest.Distance;
        }

        // Summed segment by segment in order, as the polyline walk does, so along matches it to the bit.
        private float Measure()
        {
            var walked = 0f;
            for (var i = 0; i < _lengths.Length; i++)
            {
                _walked[i] = walked;
                _lengths[i] = (Points[i + 1] - Points[i]).magnitude;
                walked += _lengths[i];
            }

            return walked;
        }

        private CellGrid CreateGrid(int segments)
        {
            var box = MapGeometry.Bounds(Points);
            var extent = math.cmax((float2)box.size);
            var average = segments > 0 ? Length / segments : 0f;
            var cellSize = math.max(average, math.max(extent / MaxCellsAcross, MinCellSize));
            var size = math.max((int2)math.ceil((float2)box.size / cellSize), 1);
            return new CellGrid(box.min, cellSize, size);
        }

        private (int[] Starts, int[] Segments) SortIntoCells(int segments)
        {
            var entries = new List<int2>();
            for (var segment = 0; segment < segments; segment++)
            {
                AddCrossedCells(segment, entries);
            }

            var starts = new int[_grid.Count + 1];
            foreach (var entry in entries)
            {
                starts[entry.x + 1]++;
            }

            for (var i = 0; i < _grid.Count; i++)
            {
                starts[i + 1] += starts[i];
            }

            var filled = (int[])starts.Clone();
            var sorted = new int[entries.Count];
            foreach (var entry in entries)
            {
                sorted[filled[entry.x]++] = entry.y;
            }

            return (starts, sorted);
        }

        private void AddCrossedCells(int segment, List<int2> entries)
        {
            var a = Points[segment];
            var b = Points[segment + 1];
            var cells = _grid.Cells(_grid.Cell((float2)Vector2.Min(a, b)), _grid.Cell((float2)Vector2.Max(a, b)));
            foreach (var cell in cells)
            {
                var center = (Vector2)_grid.Center(cell);
                if (MapGeometry.DistanceToSegment(a, b, center) <= _grid.CellSize * CellReach)
                {
                    entries.Add(new int2(_grid.Index(cell), segment));
                }
            }
        }

        private void SearchRing(Vector2 point, int2 center, int ring, ref Closest closest)
        {
            var min = center - ring;
            var max = center + ring;
            var rows = math.clamp(new int2(min.y, max.y), 0, _grid.Size.y - 1);
            for (var z = rows.x; z <= rows.y; z++)
            {
                if (z == min.y || z == max.y)
                {
                    var columns = math.clamp(new int2(min.x, max.x), 0, _grid.Size.x - 1);
                    for (var x = columns.x; x <= columns.y; x++)
                    {
                        SearchCell(point, new int2(x, z), ref closest);
                    }

                    continue;
                }

                SearchCell(point, new int2(min.x, z), ref closest);
                SearchCell(point, new int2(max.x, z), ref closest);
            }
        }

        private void SearchCell(Vector2 point, int2 cell, ref Closest closest)
        {
            if (!_grid.Contains(cell))
            {
                return;
            }

            var index = _grid.Index(cell);
            for (var i = _cellStarts[index]; i < _cellStarts[index + 1]; i++)
            {
                var segment = _cellSegments[i];
                var distance = MapGeometry.DistanceToSegment(Points[segment], Points[segment + 1], point, out var t);
                if (closest.IsBeatenBy(distance, segment))
                {
                    closest = new Closest { Distance = distance, Segment = segment, T = t };
                }
            }
        }

        private struct Closest
        {
            public float Distance;
            public int Segment;
            public float T;

            // The walk over every segment keeps the first of equal distances, so the lower segment wins a tie.
            public bool IsBeatenBy(float distance, int segment) =>
                distance < Distance || (distance == Distance && segment < Segment);
        }
    }
}
