using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>8-directional A* over the anchors a <see cref="NavPass"/> may stand on; diagonals may not cut corners.</summary>
    internal struct GridAStar
    {
        private const float Diagonal = 1.41421356f;

        private struct Record
        {
            public int Parent;
            public float Cost;
            public bool Closed;
        }

        private readonly NavPass _pass;
        private readonly NavGrid _grid;
        private readonly int2 _goal;
        private NativeHashMap<int, Record> _records;
        private OpenSet _open;

        private GridAStar(in NavPass pass, int2 goal)
        {
            _pass = pass;
            _grid = pass.Grid;
            _goal = goal;
            _records = new NativeHashMap<int, Record>(1024, Allocator.Temp);
            _open = new OpenSet(256, Allocator.Temp);
        }

        /// <summary>
        /// Fills <paramref name="path"/> with anchors from start to goal. When the goal is unreachable it leads to
        /// the explored anchor closest to the goal instead and returns false.
        /// </summary>
        public static bool Search(in NavPass pass, int2 start, int2 goal, NativeList<int2> path)
        {
            var search = new GridAStar(pass, goal);
            var last = search.Explore(start);
            search.Reconstruct(last, path);
            search.Dispose();
            return last == pass.Grid.Index(goal);
        }

        /// <summary>Returns the goal's index, or the closest cell to it found within the expansion budget.</summary>
        private int Explore(int2 start)
        {
            var goalIndex = _grid.Index(_goal);
            var best = _grid.Index(start);
            var bestHeuristic = Heuristic(start);
            _records[best] = new Record { Parent = -1 };
            _open.Push(best, bestHeuristic);

            for (var expansions = 0; expansions < NavGrid.MaxSearchExpansions && _open.TryPop(out var current);)
            {
                var record = _records[current];
                if (record.Closed)
                {
                    continue;
                }

                record.Closed = true;
                _records[current] = record;
                if (current == goalIndex)
                {
                    return current;
                }

                var cell = _grid.Cell(current);
                var heuristic = Heuristic(cell);
                if (heuristic < bestHeuristic)
                {
                    bestHeuristic = heuristic;
                    best = current;
                }

                Expand(cell, record.Cost);
                expansions++;
            }

            return best;
        }

        private void Expand(int2 cell, float cost)
        {
            var parent = _grid.Index(cell);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = cell + new int2(dx, dy);
                    if ((dx == 0 && dy == 0) || !CanStep(cell, next))
                    {
                        continue;
                    }

                    var diagonal = dx != 0 && dy != 0;
                    var nextCost = cost + (diagonal ? Diagonal : 1f);
                    var index = _grid.Index(next);
                    if (_records.TryGetValue(index, out var existing) && (existing.Closed || existing.Cost <= nextCost))
                    {
                        continue;
                    }

                    _records[index] = new Record { Parent = parent, Cost = nextCost };
                    _open.Push(index, nextCost + Heuristic(next));
                }
            }
        }

        private void Reconstruct(int last, NativeList<int2> path)
        {
            path.Clear();
            for (var index = last; index >= 0; index = _records[index].Parent)
            {
                path.Add(_grid.Cell(index));
            }

            for (int i = 0, j = path.Length - 1; i < j; i++, j--)
            {
                (path[i], path[j]) = (path[j], path[i]);
            }
        }

        private readonly bool CanStep(int2 from, int2 to) =>
            _pass.IsAnchorOpen(to) && !_pass.CutsCorner(from, to, _pass.Footprint);

        // Octile distance, nudged up slightly so ties favour cells nearer the goal (fewer expansions).
        private readonly float Heuristic(int2 cell)
        {
            var d = math.abs(_goal - cell);
            return (math.cmax(d) + (Diagonal - 1f) * math.cmin(d)) * 1.001f;
        }

        private void Dispose()
        {
            _records.Dispose();
            _open.Dispose();
        }
    }
}
