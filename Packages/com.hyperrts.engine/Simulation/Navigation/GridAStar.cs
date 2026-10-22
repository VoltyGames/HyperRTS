using System;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// 8-directional A* over the anchors a <see cref="NavAccess"/> may stand on; diagonals may not cut corners.
    /// Records live in flat arrays sized to the grid and are reused, each search stamping them with its generation.
    /// </summary>
    public struct GridAStar : IDisposable
    {
        public const float Diagonal = 1.41421356f;

        private NativeArray<int> _parents;
        private NativeArray<float> _costs;
        private NativeArray<uint> _reached;
        private NativeArray<uint> _closed;
        private NativeReference<uint> _generation;
        private OpenSet _open;
        private uint _stamp;

        public GridAStar(int cells, Allocator allocator)
        {
            _parents = new NativeArray<int>(cells, allocator, NativeArrayOptions.UninitializedMemory);
            _costs = new NativeArray<float>(cells, allocator, NativeArrayOptions.UninitializedMemory);
            _reached = new NativeArray<uint>(cells, allocator);
            _closed = new NativeArray<uint>(cells, allocator);
            _generation = new NativeReference<uint>(allocator);
            _open = new OpenSet(256, allocator);
            _stamp = 0;
        }

        public readonly bool IsCreated => _parents.IsCreated;

        /// <summary>Cells the records cover; a grid of another size needs a new search.</summary>
        public readonly int Cells => _parents.Length;

        /// <summary>
        /// Fills <paramref name="path"/> with anchors from start to goal, expanding at most <paramref name="budget"/>
        /// cells. When the goal is out of reach it leads to the explored anchor closest to the goal and returns false.
        /// </summary>
        public bool Search(in NavAccess access, int2 start, int2 goal, int budget, NativeList<int2> path,
            out int expansions)
        {
            NextGeneration();
            var last = Explore(access, start, goal, budget, out expansions);
            Reconstruct(access.Grid, last, path);
            return last == access.Grid.Index(goal);
        }

        /// <summary>Whether an agent may step between neighbouring anchors.</summary>
        public static bool CanStep(in NavAccess access, int2 from, int2 to) =>
            access.IsAnchorOpen(to) && !access.CutsCornerBetweenOpen(from, to);

        /// <summary>Octile distance, nudged up slightly so ties favour cells nearer the goal (fewer expansions).</summary>
        public static float Heuristic(int2 cell, int2 goal)
        {
            var d = math.abs(goal - cell);
            return (math.cmax(d) + (Diagonal - 1f) * math.cmin(d)) * 1.001f;
        }

        public static float StepCost(int2 from, int2 to) => from.x != to.x && from.y != to.y ? Diagonal : 1f;

        public void Dispose()
        {
            _parents.Dispose();
            _costs.Dispose();
            _reached.Dispose();
            _closed.Dispose();
            _generation.Dispose();
            _open.Dispose();
        }

        // Wrapping round would make stale stamps look current, so the records start over.
        private void NextGeneration()
        {
            _stamp = _generation.Value + 1;
            if (_stamp == 0)
            {
                for (var i = 0; i < _reached.Length; i++)
                {
                    _reached[i] = 0;
                    _closed[i] = 0;
                }

                _stamp = 1;
            }

            _generation.Value = _stamp;
            _open.Clear();
        }

        private int Explore(in NavAccess access, int2 start, int2 goal, int budget, out int expansions)
        {
            var grid = access.Grid;
            var goalIndex = grid.Index(goal);
            var best = grid.Index(start);
            var bestHeuristic = Heuristic(start, goal);
            Reach(best, -1, 0f);
            _open.Push(best, bestHeuristic);

            expansions = 0;
            while (expansions < budget && _open.TryPop(out var current))
            {
                if (_closed[current] == _stamp)
                {
                    continue;
                }

                _closed[current] = _stamp;
                if (current == goalIndex)
                {
                    return current;
                }

                var cell = grid.Cell(current);
                var heuristic = Heuristic(cell, goal);
                if (heuristic < bestHeuristic)
                {
                    bestHeuristic = heuristic;
                    best = current;
                }

                Expand(access, cell, goal);
                expansions++;
            }

            return best;
        }

        // Every expanded anchor was open when pushed, and the start comes from TryFindNearest.
        private void Expand(in NavAccess access, int2 cell, int2 goal)
        {
            var parent = access.Grid.Index(cell);
            var cost = _costs[parent];
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = cell + new int2(dx, dy);
                    if ((dx == 0 && dy == 0) || !CanStep(access, cell, next))
                    {
                        continue;
                    }

                    var nextCost = cost + StepCost(cell, next);
                    var index = access.Grid.Index(next);
                    if (_closed[index] == _stamp || (_reached[index] == _stamp && _costs[index] <= nextCost))
                    {
                        continue;
                    }

                    Reach(index, parent, nextCost);
                    _open.Push(index, nextCost + Heuristic(next, goal));
                }
            }
        }

        private void Reach(int index, int parent, float cost)
        {
            _reached[index] = _stamp;
            _parents[index] = parent;
            _costs[index] = cost;
        }

        private readonly void Reconstruct(in NavGrid grid, int last, NativeList<int2> path)
        {
            path.Clear();
            for (var index = last; index >= 0; index = _parents[index])
            {
                path.Add(grid.Cell(index));
            }

            for (int i = 0, j = path.Length - 1; i < j; i++, j--)
            {
                (path[i], path[j]) = (path[j], path[i]);
            }
        }
    }
}
