using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Abstract A* over the <see cref="NavSectors"/> graph, from the start's component to the goal's. Nodes stand at
    /// their representative cells (the start and goal at their own), and each edge costs the octile distance between
    /// its ends, so the octile heuristic stays consistent. The sectors it crosses, plus a ring, are the corridor the
    /// fine search keeps to.
    /// </summary>
    public struct SectorSearch : IDisposable
    {
        private static readonly ProfilerMarker SearchMarker = new("SectorSearch.Find");

        private struct Record
        {
            public int Parent;
            public float Cost;
            public bool Closed;
        }

        private NativeHashMap<int, Record> _records;
        private NativeList<int> _neighbours;
        private OpenSet _open;

        public SectorSearch(Allocator allocator)
        {
            _records = new NativeHashMap<int, Record>(256, allocator);
            _neighbours = new NativeList<int>(32, allocator);
            _open = new OpenSet(64, allocator);
        }

        public void Dispose()
        {
            _records.Dispose();
            _neighbours.Dispose();
            _open.Dispose();
        }

        /// <summary>
        /// Fills <paramref name="sectors"/> with the sectors the abstract path crosses, start first, and returns its
        /// length in cells; the goal must share the start's region, which an open start and goal of one region do.
        /// </summary>
        public float Find(in NavAccess access, int2 start, int2 goal, NativeList<int> sectors)
        {
            using var scope = SearchMarker.Auto();
            var graph = access.Grid.Sectors;
            var classIndex = access.ClassIndex;
            var first = graph.Component(classIndex, start);
            var last = graph.Component(classIndex, goal);
            if (first < 0 || last < 0 || graph.Regions[first] != graph.Regions[last])
            {
                throw new InvalidOperationException("Abstract search between cells of different regions.");
            }

            _records.Clear();
            _open.Clear();
            _records[first] = new Record { Parent = -1 };
            _open.Push(first, GridAStar.Heuristic(start, goal));
            while (_open.TryPop(out var node))
            {
                var record = _records[node];
                if (record.Closed)
                {
                    continue;
                }

                record.Closed = true;
                _records[node] = record;
                if (node == last)
                {
                    break;
                }

                Expand(access, node, record.Cost, first, last, start, goal);
            }

            Collect(graph, last, sectors);
            return _records[last].Cost;
        }

        private void Expand(in NavAccess access, int node, float cost, int first, int last, int2 start, int2 goal)
        {
            var graph = access.Grid.Sectors;
            var at = Position(graph, access.ClassIndex, node, first, last, start, goal);
            _neighbours.Clear();
            graph.Neighbours(access.ClassIndex, node, _neighbours);
            foreach (var next in _neighbours)
            {
                var to = Position(graph, access.ClassIndex, next, first, last, start, goal);
                var nextCost = cost + GridAStar.Octile(at, to);
                var seen = _records.TryGetValue(next, out var record);
                if (seen && (record.Closed || record.Cost <= nextCost))
                {
                    continue;
                }

                _records[next] = new Record { Parent = node, Cost = nextCost };
                _open.Push(next, nextCost + GridAStar.Heuristic(to, goal));
            }
        }

        private static int2 Position(in NavSectors graph, int classIndex, int node, int first, int last, int2 start,
            int2 goal)
        {
            if (node == first)
            {
                return start;
            }

            return node == last ? goal : graph.Representative(classIndex, node);
        }

        // Sectors from the start's to the goal's, each once in a row.
        private readonly void Collect(in NavSectors graph, int last, NativeList<int> sectors)
        {
            var begin = sectors.Length;
            for (var node = last; node >= 0; node = _records[node].Parent)
            {
                var sector = graph.Owners[node];
                if (sectors.Length == begin || sectors[sectors.Length - 1] != sector)
                {
                    sectors.Add(sector);
                }
            }

            for (int i = begin, j = sectors.Length - 1; i < j; i++, j--)
            {
                (sectors[i], sectors[j]) = (sectors[j], sectors[i]);
            }
        }
    }
}
