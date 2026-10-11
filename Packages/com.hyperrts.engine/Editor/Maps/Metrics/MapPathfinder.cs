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
        private const float Diagonal = 1.41421356f;

        /// <summary>The shortest walkable path, or null when the goal can't be reached.</summary>
        public static MapPath Find(MapNavGrid nav, int2 start, int2 goal, bool[] closed = null)
        {
            var grid = nav.Grid;
            var cost = new Dictionary<int, float> { [grid.Index(start)] = 0f };
            var parent = new Dictionary<int, int> { [grid.Index(start)] = -1 };
            var done = new HashSet<int>();
            var open = new SortedSet<(float Priority, int Index)> { (Heuristic(start, goal), grid.Index(start)) };
            var goalIndex = grid.Index(goal);
            var expansions = 0;
            while (open.Count > 0)
            {
                var (_, current) = open.Min;
                open.Remove(open.Min);
                if (!done.Add(current))
                {
                    continue;
                }

                if (current == goalIndex)
                {
                    return Build(nav, parent, goalIndex, cost[goalIndex], expansions);
                }

                expansions++;
                Expand(nav, grid.Cell(current), cost[current], goal, closed, cost, parent, open);
            }

            return null;
        }

        private static void Expand(MapNavGrid nav, int2 cell, float baseCost, int2 goal, bool[] closed,
            Dictionary<int, float> cost, Dictionary<int, int> parent, SortedSet<(float, int)> open)
        {
            var grid = nav.Grid;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = cell + new int2(dx, dy);
                    if ((dx == 0 && dy == 0) || !Open(nav, next, closed))
                    {
                        continue;
                    }

                    var diagonal = dx != 0 && dy != 0;
                    if (diagonal && (!Open(nav, new int2(cell.x, next.y), closed) || !Open(nav, new int2(next.x, cell.y), closed)))
                    {
                        continue;
                    }

                    var index = grid.Index(next);
                    var nextCost = baseCost + (diagonal ? Diagonal : 1f);
                    if (cost.TryGetValue(index, out var known) && known <= nextCost)
                    {
                        continue;
                    }

                    cost[index] = nextCost;
                    parent[index] = grid.Index(cell);
                    open.Add((nextCost + Heuristic(next, goal), index));
                }
            }
        }

        private static bool Open(MapNavGrid nav, int2 cell, bool[] closed)
        {
            if (!nav.Grid.InBounds(cell) || !nav.IsWalkable(cell))
            {
                return false;
            }

            return closed == null || !closed[nav.Grid.Index(cell)];
        }

        private static MapPath Build(MapNavGrid nav, Dictionary<int, int> parent, int goal, float cost, int expansions)
        {
            var cells = new List<int2>();
            for (var index = goal; index >= 0; index = parent[index])
            {
                cells.Add(nav.Grid.Cell(index));
            }

            cells.Reverse();
            return new MapPath(cells, cost * nav.Grid.CellSize, expansions);
        }

        private static float Heuristic(int2 cell, int2 goal)
        {
            var d = math.abs(goal - cell);
            return (math.cmax(d) + (Diagonal - 1f) * math.cmin(d)) * 1.001f;
        }
    }
}
