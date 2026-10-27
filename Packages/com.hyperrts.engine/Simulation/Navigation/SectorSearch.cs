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

        // This search's ends: the start's and goal's components, which stand at their own cells.
        private int _first;
        private int _last;
        private int2 _start;
        private int2 _goal;

        public SectorSearch(Allocator allocator)
        {
            _records = new NativeHashMap<int, Record>(256, allocator);
            _neighbours = new NativeList<int>(32, allocator);
            _open = new OpenSet(64, allocator);
            _first = -1;
            _last = -1;
            _start = default;
            _goal = default;
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
            _first = graph.Component(classIndex, start);
            _last = graph.Component(classIndex, goal);
            _start = start;
            _goal = goal;
            if (_first < 0 || _last < 0 || graph.Regions[_first] != graph.Regions[_last])
            {
                throw new InvalidOperationException("Abstract search between cells of different regions.");
            }

            _records.Clear();
            _open.Clear();
            _records[_first] = new Record { Parent = -1 };
            _open.Push(_first, GridAStar.Heuristic(start, goal));
            while (_open.TryPop(out var node))
            {
                var record = _records[node];
                if (record.Closed)
                {
                    continue;
                }

                record.Closed = true;
                _records[node] = record;
                if (node == _last)
                {
                    break;
                }

                Expand(graph, classIndex, node, record.Cost);
            }

            Collect(graph, _last, sectors);
            return _records[_last].Cost;
        }

        private void Expand(in NavSectors graph, int classIndex, int node, float cost)
        {
            var at = Position(graph, classIndex, node);
            _neighbours.Clear();
            graph.Neighbours(classIndex, node, _neighbours);
            foreach (var next in _neighbours)
            {
                var to = Position(graph, classIndex, next);
                var nextCost = cost + GridAStar.Octile(at, to);
                var seen = _records.TryGetValue(next, out var record);
                if (seen && (record.Closed || record.Cost <= nextCost))
                {
                    continue;
                }

                _records[next] = new Record { Parent = node, Cost = nextCost };
                _open.Push(next, nextCost + GridAStar.Heuristic(to, _goal));
            }
        }

        private readonly int2 Position(in NavSectors graph, int classIndex, int node)
        {
            if (node == _first)
            {
                return _start;
            }

            return node == _last ? _goal : graph.Representative(classIndex, node);
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
