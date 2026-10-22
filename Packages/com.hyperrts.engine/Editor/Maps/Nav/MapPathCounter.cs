using System.Collections.Generic;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
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
            // Corridors close cells of a copy, so the map's own grid stays as built.
            var grid = nav.Grid;
            grid.Cells = new NativeArray<byte>(nav.Grid.Cells, Allocator.Persistent);
            var paths = new List<MapPath>();
            var corridor = (int)math.ceil(corridorWidth * 0.5f / grid.CellSize);
            var keepOpen = baseRadius / grid.CellSize;
            try
            {
                while (paths.Count < max)
                {
                    var path = MapPathfinder.Find(ref nav.Search, grid, start, goal);
                    if (path == null)
                    {
                        break;
                    }

                    paths.Add(path);
                    foreach (var cell in path.Cells)
                    {
                        if (math.distance(cell, start) > keepOpen && math.distance(cell, goal) > keepOpen)
                        {
                            Close(grid, cell, corridor);
                        }
                    }
                }
            }
            finally
            {
                grid.Cells.Dispose();
            }

            return paths;
        }

        /// <summary>Every cell reachable by ground from <paramref name="start"/>: its region one cell wide.</summary>
        public static bool[] Flood(MapNavGrid nav, int2 start)
        {
            var grid = nav.Grid;
            var region = grid.Region(NavLayer.Ground, 1, start);
            var reached = new bool[grid.Cells.Length];
            for (var i = 0; i < reached.Length; i++)
            {
                reached[i] = region != 0 && grid.Region(NavLayer.Ground, 1, grid.Cell(i)) == region;
            }

            reached[grid.Index(start)] = true;
            return reached;
        }

        private static void Close(NavGrid grid, int2 center, int radius)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    var cell = center + new int2(x, y);
                    if (x * x + y * y <= radius * radius && grid.InBounds(cell))
                    {
                        grid.Cells[grid.Index(cell)] = (byte)NavSurface.Blocked;
                    }
                }
            }
        }
    }
}
