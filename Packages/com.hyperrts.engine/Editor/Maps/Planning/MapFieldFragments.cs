using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Sizes of the connected pieces of each field (roads and rivers cut fields into pieces), so slivers too small to
    /// farm can be left to the margin.
    /// </summary>
    public static class MapFieldFragments
    {
        /// <summary>For every cell, the cell count of the four-connected piece of its field it belongs to (0 off fields).</summary>
        public static int[] Sizes(int[] fields, int width, int height)
        {
            var sizes = new int[fields.Length];
            var piece = new List<int>();
            var queue = new Queue<int>();
            for (var start = 0; start < fields.Length; start++)
            {
                if (fields[start] < 0 || sizes[start] != 0)
                {
                    continue;
                }

                Flood(fields, width, height, start, sizes, piece, queue);
                foreach (var cell in piece)
                {
                    sizes[cell] = piece.Count;
                }
            }

            return sizes;
        }

        private static void Flood(int[] fields, int width, int height, int start, int[] sizes, List<int> piece,
            Queue<int> queue)
        {
            piece.Clear();
            var field = fields[start];
            sizes[start] = -1;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                piece.Add(cell);
                var x = cell % width;
                var z = cell / width;
                Visit(fields, sizes, queue, field, x > 0 ? cell - 1 : -1);
                Visit(fields, sizes, queue, field, x < width - 1 ? cell + 1 : -1);
                Visit(fields, sizes, queue, field, z > 0 ? cell - width : -1);
                Visit(fields, sizes, queue, field, z < height - 1 ? cell + width : -1);
            }
        }

        private static void Visit(int[] fields, int[] sizes, Queue<int> queue, int field, int cell)
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
