using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// The cost to reach one goal cell from every cell of a <see cref="NavCorridor"/>, by Dijkstra with the steps and
    /// costs of <see cref="GridAStar"/>: 8 directions, no corner cutting, dearer near walls. Following it downhill
    /// gives each start an optimal path with no search of its own.
    /// </summary>
    public struct FlowField : IDisposable
    {
        private static readonly ProfilerMarker BuildMarker = new("FlowField.Build");
        private static readonly ProfilerMarker TraceMarker = new("FlowField.Trace");

        public NavCorridor Corridor;
        public int2 Goal;
        private UnsafeList<float> _costs;

        /// <summary>A field over the corridor, which it owns; <see cref="Build"/> fills it.</summary>
        public FlowField(NavCorridor corridor, int2 goal, Allocator allocator)
        {
            Corridor = corridor;
            Goal = goal;
            _costs = new UnsafeList<float>(corridor.Cells, allocator);
            _costs.Resize(corridor.Cells);
        }

        public void Dispose()
        {
            Corridor.Dispose();
            _costs.Dispose();
        }

        /// <summary>The cost from a cell to the goal; infinity outside the corridor or out of reach.</summary>
        public readonly float Cost(int2 cell)
        {
            var index = Corridor.Index(cell);
            return index < 0 ? float.PositiveInfinity : _costs[index];
        }

        public readonly bool Reaches(int2 cell) => Cost(cell) < float.PositiveInfinity;

        /// <summary>Fills the costs from the goal outward; returns the cells settled.</summary>
        public int Build(in NavAccess access)
        {
            using var scope = BuildMarker.Auto();
            for (var i = 0; i < _costs.Length; i++)
            {
                _costs[i] = float.PositiveInfinity;
            }

            var settled = new NativeArray<bool>(_costs.Length, Allocator.Temp);
            var open = new OpenSet(1024, Allocator.Temp);
            var goal = Corridor.Index(Goal);
            _costs[goal] = 0f;
            open.Push(goal, 0f);
            var count = 0;
            while (open.TryPop(out var current))
            {
                if (settled[current])
                {
                    continue;
                }

                settled[current] = true;
                count++;
                Relax(access, current, ref open);
            }

            open.Dispose();
            settled.Dispose();
            return count;
        }

        // Settles backwards: a neighbour pays its step onto this cell, as a search from it would.
        private void Relax(in NavAccess access, int index, ref OpenSet open)
        {
            var cell = Corridor.Cell(index);
            var cost = _costs[index];
            var block = new CellBlock(access, cell);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var offset = new int2(dx, dy);
                    var at = Corridor.Index(index, cell, offset);
                    if ((dx == 0 && dy == 0) || at < 0 || !block.CanStepIn(offset))
                    {
                        continue;
                    }

                    var next = cost + block.CostIn(offset);
                    if (next < _costs[at])
                    {
                        _costs[at] = next;
                        open.Push(at, next);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the cells from <paramref name="from"/> down the field to the goal. False when the cell is out of reach,
        /// or a cell closed since the build (scenery doesn't bump the grid version) leaves no step downhill.
        /// </summary>
        public readonly bool Trace(in NavAccess access, int2 from, NativeList<int2> cells)
        {
            using var scope = TraceMarker.Auto();
            if (!Reaches(from))
            {
                return false;
            }

            cells.Add(from);
            var current = from;
            while (!current.Equals(Goal))
            {
                if (!TryStepDown(access, current, out current))
                {
                    return false;
                }

                cells.Add(current);
            }

            return true;
        }

        // The neighbour on the cheapest way to the goal; its cost is always lower, so the walk ends.
        private readonly bool TryStepDown(in NavAccess access, int2 cell, out int2 best)
        {
            var here = Cost(cell);
            var bestCost = float.PositiveInfinity;
            best = cell;
            var block = new CellBlock(access, cell);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var offset = new int2(dx, dy);
                    var next = cell + offset;
                    var cost = Cost(next);
                    if ((dx == 0 && dy == 0) || cost >= here || !block.CanStepOut(offset))
                    {
                        continue;
                    }

                    var total = cost + block.CostOut(offset);
                    if (total < bestCost)
                    {
                        bestCost = total;
                        best = next;
                    }
                }
            }

            return bestCost < float.PositiveInfinity;
        }
    }
}
