using System.Collections.Generic;
using Unity.Mathematics;

namespace HyperRTS.Editor.Maps.Nav
{
    /// <summary>
    /// Counts separate attack paths: find the shortest path, close a vehicle-column corridor along it (outside both
    /// bases) and search again until no path is left.
    /// </summary>
    public static class MapPathCounter
    {
        public static List<MapPath> Count(MapNavGrid nav, int2 start, int2 goal, float corridorWidth, float baseRadius,
            int max)
        {
            var grid = nav.Grid;
            var closed = new bool[grid.Cells.Length];
            var paths = new List<MapPath>();
            var corridor = (int)math.ceil(corridorWidth * 0.5f / grid.CellSize);
            var keepOpen = baseRadius / grid.CellSize;
            while (paths.Count < max)
            {
                var path = MapPathfinder.Find(nav, start, goal, closed);
                if (path == null)
                {
                    break;
                }

                paths.Add(path);
                foreach (var cell in path.Cells)
                {
                    if (math.distance(cell, start) > keepOpen && math.distance(cell, goal) > keepOpen)
                    {
                        Close(grid.Size, closed, cell, corridor);
                    }
                }
            }

            return paths;
        }

        /// <summary>Every cell reachable by ground from <paramref name="start"/>, by the pathfinders' step rules.</summary>
        public static bool[] Flood(MapNavGrid nav, int2 start)
        {
            var grid = nav.Grid;
            var reached = new bool[grid.Cells.Length];
            var queue = new Queue<int2>();
            reached[grid.Index(start)] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var next = cell + new int2(dx, dy);
                        if (!grid.InBounds(next) || reached[grid.Index(next)] || !MapStep.CanStep(nav, cell, next, null))
                        {
                            continue;
                        }

                        reached[grid.Index(next)] = true;
                        queue.Enqueue(next);
                    }
                }
            }

            return reached;
        }

        private static void Close(int2 size, bool[] closed, int2 center, int radius)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    var cell = center + new int2(x, y);
                    if (x * x + y * y <= radius * radius && math.all(cell >= 0 & cell < size))
                    {
                        closed[cell.y * size.x + cell.x] = true;
                    }
                }
            }
        }
    }
}
