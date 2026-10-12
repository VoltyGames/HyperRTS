using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace HyperRTS.Editor.Maps.Metrics
{
    /// <summary>
    /// Unbudgeted 8-way A* for map metrics, with the runtime's heuristic and corner rule, plus closed cells (the
    /// corridors already counted as attack paths).
    /// </summary>
    public static class MapPathfinder
    {
        /// <summary>The shortest walkable path, or null when the goal can't be reached.</summary>
        public static MapPath Find(MapNavGrid nav, int2 start, int2 goal, bool[] closed = null)
        {
            var grid = nav.Grid;
            var cost = new float[grid.Cells.Length];
            Array.Fill(cost, float.PositiveInfinity);
            var parent = new int[grid.Cells.Length];
            var done = new bool[grid.Cells.Length];
            var startIndex = grid.Index(start);
            cost[startIndex] = 0f;
            parent[startIndex] = -1;
            var open = new MapHeap();
            open.Push(MapStep.Heuristic(start, goal), startIndex);
            var goalIndex = grid.Index(goal);
            var expansions = 0;
            while (open.Count > 0)
            {
                var (_, current) = open.Pop();
                if (done[current])
                {
                    continue;
                }

                done[current] = true;
                if (current == goalIndex)
                {
                    return Build(nav, parent, goalIndex, cost[goalIndex], expansions);
                }

                expansions++;
                Expand(nav, grid.Cell(current), goal, closed, cost, parent, open);
            }

            return null;
        }

        private static void Expand(MapNavGrid nav, int2 cell, int2 goal, bool[] closed, float[] cost, int[] parent,
            MapHeap open)
        {
            var grid = nav.Grid;
            var currentIndex = grid.Index(cell);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = cell + new int2(dx, dy);
                    if ((dx == 0 && dy == 0) || !MapStep.CanStep(nav, cell, next, closed))
                    {
                        continue;
                    }

                    var index = grid.Index(next);
                    var nextCost = cost[currentIndex] + MapStep.Cost(cell, next);
                    if (cost[index] <= nextCost)
                    {
                        continue;
                    }

                    cost[index] = nextCost;
                    parent[index] = currentIndex;
                    open.Push(nextCost + MapStep.Heuristic(next, goal), index);
                }
            }
        }

        private static MapPath Build(MapNavGrid nav, int[] parent, int goal, float cost, int expansions)
        {
            var cells = new List<int2>();
            for (var index = goal; index >= 0; index = parent[index])
            {
                cells.Add(nav.Grid.Cell(index));
            }

            cells.Reverse();
            return new MapPath(cells, cost * nav.Grid.CellSize, expansions);
        }
    }
}
