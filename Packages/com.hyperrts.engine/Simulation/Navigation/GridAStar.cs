using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// 8-directional A* over the open cells of a <see cref="NavCorridor"/>. Records are sized to the corridor, so each
    /// search or worker owns one.
    /// </summary>
    public struct GridAStar : IDisposable
    {
        public const float Diagonal = 1.41421356f;

        private const byte Reached = 1;
        private const byte Closed = 2;

        // Cuts cross-map expansions about two thirds for paths about 0.1% longer (NavBenchmarkTests).
        private const float HeuristicWeight = 1.25f;

        private static readonly ProfilerMarker SearchMarker = new("GridAStar.Search");

        // Unchecked, as these are read and written for every neighbour of every expansion.
        private UnsafeList<int> _parents;
        private UnsafeList<float> _costs;
        private UnsafeList<byte> _marks;
        private OpenSet _open;
        private readonly NavCorridor _corridor;
        private int2 _goal;
        private int _goalIndex;

        /// <summary>Records for the corridor, which it searches but doesn't own.</summary>
        public GridAStar(in NavCorridor corridor, Allocator allocator)
        {
            var cells = corridor.Cells;
            _corridor = corridor;
            _goal = default;
            _goalIndex = -1;
            _parents = new UnsafeList<int>(cells, allocator);
            _parents.Resize(cells);
            _costs = new UnsafeList<float>(cells, allocator);
            _costs.Resize(cells);
            _marks = new UnsafeList<byte>(cells, allocator);
            _marks.Resize(cells, NativeArrayOptions.ClearMemory);
            _open = new OpenSet(256, allocator);
        }

        /// <summary>
        /// Fills <paramref name="path"/> with cells from start to goal, both inside the corridor; false, with no path,
        /// when the goal is out of the start's reach there. Never stops early.
        /// </summary>
        public bool Search(in NavAccess access, int2 start, int2 goal, NativeList<int2> path, out int expansions)
        {
            using var scope = SearchMarker.Auto();
            for (var i = 0; i < _marks.Length; i++)
            {
                _marks[i] = 0;
            }

            _open.Clear();
            path.Clear();
            _goal = goal;
            _goalIndex = _corridor.Index(goal);
            var reached = Explore(access, start, out expansions);
            if (reached)
            {
                Reconstruct(_goalIndex, path);
            }

            return reached;
        }

        /// <summary>Whether an agent may step between neighbouring cells: onto an open one, past open corners.</summary>
        public static bool CanStep(in NavAccess access, int2 from, int2 to)
        {
            if (!access.IsOpen(to))
            {
                return false;
            }

            var diagonal = from.x != to.x && from.y != to.y;
            return !diagonal || (access.IsOpen(new int2(from.x, to.y)) && access.IsOpen(new int2(to.x, from.y)));
        }

        /// <summary>Octile distance, nudged up slightly so ties favour cells nearer the goal (fewer expansions).</summary>
        public static float Heuristic(int2 cell, int2 goal) => Octile(cell, goal) * 1.001f;

        /// <summary>The shortest eight-way distance between two cells, in cells.</summary>
        public static float Octile(int2 a, int2 b)
        {
            var d = math.abs(b - a);
            return math.cmax(d) + (Diagonal - 1f) * math.cmin(d);
        }

        /// <summary>A step's length in cells.</summary>
        public static float StepCost(int2 from, int2 to) => from.x != to.x && from.y != to.y ? Diagonal : 1f;

        /// <summary>What the search pays for a step: its length, dearer near walls.</summary>
        public static float Cost(in NavAccess access, int2 from, int2 to) => StepCost(from, to) * access.CellCost(to);

        public void Dispose()
        {
            _parents.Dispose();
            _costs.Dispose();
            _marks.Dispose();
            _open.Dispose();
        }

        private bool Explore(in NavAccess access, int2 start, out int expansions)
        {
            var first = _corridor.Index(start);
            Reach(first, -1, 0f);
            _open.Push(first, Heuristic(start, _goal) * HeuristicWeight);
            expansions = 0;
            while (_open.TryPop(out var current))
            {
                if (_marks[current] == Closed)
                {
                    continue;
                }

                _marks[current] = Closed;
                if (current == _goalIndex)
                {
                    return true;
                }

                Expand(access, current);
                expansions++;
            }

            return false;
        }

        // Every expanded cell was open when pushed, and the start comes from TryFindNearest.
        private void Expand(in NavAccess access, int parent)
        {
            var cell = _corridor.Cell(parent);
            var cost = _costs[parent];
            var block = new CellBlock(access, cell);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var offset = new int2(dx, dy);
                    var index = _corridor.Index(parent, cell, offset);
                    if ((dx == 0 && dy == 0) || index < 0 || _marks[index] == Closed || !block.CanStepOut(offset))
                    {
                        continue;
                    }

                    var next = cell + offset;
                    var nextCost = cost + block.CostOut(offset);
                    if (_marks[index] == Reached && _costs[index] <= nextCost)
                    {
                        continue;
                    }

                    Reach(index, parent, nextCost);
                    _open.Push(index, nextCost + Heuristic(next, _goal) * HeuristicWeight);
                }
            }
        }

        private void Reach(int index, int parent, float cost)
        {
            _marks[index] = Reached;
            _parents[index] = parent;
            _costs[index] = cost;
        }

        private readonly void Reconstruct(int last, NativeList<int2> path)
        {
            for (var index = last; index >= 0; index = _parents[index])
            {
                path.Add(_corridor.Cell(index));
            }

            for (int i = 0, j = path.Length - 1; i < j; i++, j--)
            {
                (path[i], path[j]) = (path[j], path[i]);
            }
        }
    }
}
