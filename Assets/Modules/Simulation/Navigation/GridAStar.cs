using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>8-directional A* over a <see cref="NavGrid"/>; diagonals may not cut blocked corners.</summary>
    internal static class GridAStar
    {
        /// <summary>Caps one search so a hopeless request can't stall a worker thread.</summary>
        public const int MaxExpansions = 20000;

        private const float Diagonal = 1.41421356f;

        private struct Record
        {
            public int Parent;
            public float Cost;
            public bool Closed;
        }

        /// <summary>
        /// Fills <paramref name="path"/> with cells from start to goal. When the goal is unreachable it leads to
        /// the explored cell closest to the goal instead and returns false.
        /// </summary>
        public static bool Search(in NavGrid grid, int2 start, int2 goal, NativeList<int2> path)
        {
            var records = new NativeHashMap<int, Record>(1024, Allocator.Temp);
            var open = new OpenSet(256, Allocator.Temp);
            var last = Explore(grid, start, goal, ref records, ref open);
            Reconstruct(grid, records, last, path);
            records.Dispose();
            open.Dispose();
            return last == grid.Index(goal);
        }

        /// <summary>Returns the goal's index, or the closest cell to it found within the expansion budget.</summary>
        private static int Explore(in NavGrid grid, int2 start, int2 goal, ref NativeHashMap<int, Record> records,
            ref OpenSet open)
        {
            var goalIndex = grid.Index(goal);
            var best = grid.Index(start);
            var bestHeuristic = Heuristic(start, goal);
            records[best] = new Record { Parent = -1 };
            open.Push(best, bestHeuristic);

            for (var expansions = 0; expansions < MaxExpansions && open.TryPop(out var current);)
            {
                var record = records[current];
                if (record.Closed)
                {
                    continue;
                }

                record.Closed = true;
                records[current] = record;
                if (current == goalIndex)
                {
                    return current;
                }

                var cell = new int2(current % grid.Size.x, current / grid.Size.x);
                var heuristic = Heuristic(cell, goal);
                if (heuristic < bestHeuristic)
                {
                    bestHeuristic = heuristic;
                    best = current;
                }

                Expand(grid, cell, record.Cost, goal, ref records, ref open);
                expansions++;
            }

            return best;
        }

        private static void Expand(in NavGrid grid, int2 cell, float cost, int2 goal,
            ref NativeHashMap<int, Record> records, ref OpenSet open)
        {
            var parent = grid.Index(cell);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = cell + new int2(dx, dy);
                    if ((dx == 0 && dy == 0) || !grid.IsWalkable(next))
                    {
                        continue;
                    }

                    var diagonal = dx != 0 && dy != 0;
                    if (diagonal && (!grid.IsWalkable(cell + new int2(dx, 0)) || !grid.IsWalkable(cell + new int2(0, dy))))
                    {
                        continue;
                    }

                    var nextCost = cost + (diagonal ? Diagonal : 1f);
                    var index = grid.Index(next);
                    if (records.TryGetValue(index, out var existing) && (existing.Closed || existing.Cost <= nextCost))
                    {
                        continue;
                    }

                    records[index] = new Record { Parent = parent, Cost = nextCost };
                    open.Push(index, nextCost + Heuristic(next, goal));
                }
            }
        }

        private static void Reconstruct(in NavGrid grid, NativeHashMap<int, Record> records, int last,
            NativeList<int2> path)
        {
            path.Clear();
            for (var index = last; index >= 0; index = records[index].Parent)
            {
                path.Add(new int2(index % grid.Size.x, index / grid.Size.x));
            }

            for (int i = 0, j = path.Length - 1; i < j; i++, j--)
            {
                (path[i], path[j]) = (path[j], path[i]);
            }
        }

        // Octile distance, nudged up slightly so ties favour cells nearer the goal (fewer expansions).
        private static float Heuristic(int2 from, int2 to)
        {
            var d = math.abs(to - from);
            return (math.cmax(d) + (Diagonal - 1f) * math.cmin(d)) * 1.001f;
        }
    }
}
