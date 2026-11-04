using HyperRTS.Simulation.Common.Cells;
using System;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Sectors;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation.Fields
{
    /// <summary>
    /// The cost to reach one goal cell from every cell of a <see cref="NavCorridor"/>, so each start follows it
    /// downhill on an optimal path with no search of its own.
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
            var search = new ReverseDijkstra(_costs.Length, Allocator.Temp);
            // A step costs its length in cells, raised near walls, so never less than one.
            search.Reset(_costs.Length, 1f);
            search.Seed(Corridor.Index(Goal));
            var graph = new CorridorGraph { Access = access, Corridor = Corridor };
            var count = search.Settle(ref graph);
            _costs.CopyFrom(search.Costs);
            search.Dispose();
            return count;
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
            var block = new CellNeighbourhood(access, cell);
            foreach (var offset in new NeighbourOffsets())
            {
                var next = cell + offset;
                var cost = Cost(next);
                if (cost >= here || !block.CanStepOut(offset))
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

            return bestCost < float.PositiveInfinity;
        }

        // Settles backwards: a neighbour pays its step onto this cell, as a search from it would.
        private struct CorridorGraph : IReverseGraph
        {
            public NavAccess Access;
            public NavCorridor Corridor;

            public void Relax(int index, ref ReverseDijkstra search)
            {
                var cell = Corridor.Cell(index);
                var cost = search.Costs[index];
                var block = new CellNeighbourhood(Access, cell);
                foreach (var offset in new NeighbourOffsets())
                {
                    var at = Corridor.Index(index, cell, offset);
                    if (at >= 0 && block.CanStepIn(offset))
                    {
                        search.Offer(at, cost + block.CostIn(offset));
                    }
                }
            }
        }
    }
}
