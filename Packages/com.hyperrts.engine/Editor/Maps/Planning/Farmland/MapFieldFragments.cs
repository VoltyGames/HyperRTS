using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;

namespace HyperRTS.Editor.Maps.Planning.Farmland
{
    /// <summary>
    /// The connected pieces of each field (roads, rivers and ravines cut fields into pieces), measured so slivers can be
    /// left to the margin: how many cells a piece has, and how far its innermost cell lies from the piece's edge (half
    /// the widest strip across it).
    /// </summary>
    public static class MapFieldFragments
    {
        /// <summary>
        /// For every cell, its piece's cell count and its piece's largest <paramref name="edge"/> distance (0 off fields).
        /// </summary>
        public static (MapValues<int> Sizes, MapValues<float> Reach) Measure(MapValues<int> fields, MapValues<float> edge)
        {
            var sizes = new MapValues<int>(fields.Grid);
            var reach = new MapValues<float>(fields.Grid);
            var piece = new List<int>();
            var queue = new Queue<int>();
            for (var start = 0; start < fields.Grid.Count; start++)
            {
                if (fields[start] < 0 || sizes[start] != 0)
                {
                    continue;
                }

                Flood(fields, start, sizes.Values, piece, queue);
                var deepest = 0f;
                foreach (var cell in piece)
                {
                    deepest = edge[cell] > deepest ? edge[cell] : deepest;
                }

                foreach (var cell in piece)
                {
                    sizes[cell] = piece.Count;
                    reach[cell] = deepest;
                }
            }

            return (sizes, reach);
        }

        private static void Flood(MapValues<int> fields, int start, int[] sizes, List<int> piece, Queue<int> queue)
        {
            var grid = fields.Grid;
            var width = grid.Width;
            piece.Clear();
            var field = fields[start];
            sizes[start] = -1;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                piece.Add(cell);
                var at = grid.Cells.Cell(cell);
                Visit(fields, sizes, queue, field, at.x > 0 ? cell - 1 : -1);
                Visit(fields, sizes, queue, field, at.x < width - 1 ? cell + 1 : -1);
                Visit(fields, sizes, queue, field, at.y > 0 ? cell - width : -1);
                Visit(fields, sizes, queue, field, at.y < grid.Height - 1 ? cell + width : -1);
            }
        }

        private static void Visit(MapValues<int> fields, int[] sizes, Queue<int> queue, int field, int cell)
        {
            if (cell < 0 || sizes[cell] != 0 || fields[cell] != field)
            {
                return;
            }

            sizes[cell] = -1;
            queue.Enqueue(cell);
        }
    }
}
