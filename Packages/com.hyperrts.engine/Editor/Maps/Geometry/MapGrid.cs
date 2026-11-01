using System;
using HyperRTS.Simulation.Common;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>
    /// A grid of samples over a map rectangle, row by row from the south-west: on cell corners spanning the bounds
    /// (heights) or at cell centres (land). <see cref="Cells"/> counts the samples, one unit each.
    /// </summary>
    public readonly struct MapGrid
    {
        public readonly CellGrid Cells;
        public readonly Rect Bounds;

        /// <summary>Metres between samples per axis: a heightmap stretches on a map that isn't square.</summary>
        public readonly Vector2 Spacing;

        public readonly bool AtCorners;

        private MapGrid(Rect bounds, Vector2 spacing, int2 size, bool atCorners)
        {
            Cells = new CellGrid(float2.zero, 1f, size);
            Bounds = bounds;
            Spacing = spacing;
            AtCorners = atCorners;
        }

        /// <summary>Samples on the corners of a square grid, the first and last on the edges of the bounds.</summary>
        public static MapGrid Corners(Rect bounds, int resolution) =>
            new(bounds, bounds.size / (resolution - 1), new int2(resolution, resolution), true);

        /// <summary>Samples at the centres of square cells covering the bounds, the last row and column overhanging.</summary>
        public static MapGrid Centres(Rect bounds, float cellSize) => new(bounds, new Vector2(cellSize, cellSize),
            new int2(Mathf.CeilToInt(bounds.width / cellSize), Mathf.CeilToInt(bounds.height / cellSize)), false);

        public int Width => Cells.Size.x;

        public int Height => Cells.Size.y;

        public int Count => Cells.Count;

        public int Index(int x, int z) => Cells.Index(new int2(x, z));

        public bool Contains(int x, int z) => Cells.Contains(new int2(x, z));

        public Vector2 Position(int x, int z) => AtCorners
            ? Bounds.min + Vector2.Scale(new Vector2(x, z), Spacing)
            : Bounds.min + new Vector2((x + 0.5f) * Spacing.x, (z + 0.5f) * Spacing.y);

        public Vector2 Position(int index)
        {
            var cell = Cells.Cell(index);
            return Position(cell.x, cell.y);
        }

        /// <summary>A point in samples from the first one, so samples sit on whole numbers.</summary>
        public Vector2 Local(Vector2 point)
        {
            var local = (point - Bounds.min) / Spacing;
            return AtCorners ? local : local - new Vector2(0.5f, 0.5f);
        }

        /// <summary>The cell a point falls in, counted from the south-west edge of the bounds; false off the grid.</summary>
        public bool TryCell(Vector2 point, out int x, out int z)
        {
            var local = (point - Bounds.min) / Spacing;
            x = Mathf.FloorToInt(local.x);
            z = Mathf.FloorToInt(local.y);
            return Contains(x, z);
        }

        /// <summary>The four samples round a point and where it sits between them, clamped to the grid.</summary>
        public MapBilinear Bilinear(Vector2 point)
        {
            var local = Local(point);
            var x = Mathf.Clamp(local.x, 0f, Width - 1);
            var z = Mathf.Clamp(local.y, 0f, Height - 1);
            var x0 = Mathf.Min((int)x, Width - 2);
            var z0 = Mathf.Min((int)z, Height - 2);
            return new MapBilinear(x0, z0, x - x0, z - z0);
        }

        /// <summary>Calls back for every sample in a rectangle of the map, widened to whole samples, with its position.</summary>
        public void ForEachIn(Rect area, Action<int, int, Vector2> visit)
        {
            var min = Vector2Int.FloorToInt((area.min - Bounds.min) / Spacing);
            var max = Vector2Int.CeilToInt((area.max - Bounds.min) / Spacing);
            for (var z = Mathf.Max(0, min.y); z <= Mathf.Min(Height - 1, max.y); z++)
            {
                for (var x = Mathf.Max(0, min.x); x <= Mathf.Min(Width - 1, max.x); x++)
                {
                    visit(x, z, Position(x, z));
                }
            }
        }
    }
}
