using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Fields
{
    /// <summary>
    /// Dijkstra from a goal outward over cells numbered 0 to the count, settling each cell's cost to the goal. Buckets
    /// half the cheapest step wide keep settles exact; the graph decides each cell's neighbours and costs.
    /// </summary>
    public struct ReverseDijkstra : IDisposable
    {
        public UnsafeList<float> Costs;

        /// <summary>Cells done, or closed before the search starts.</summary>
        public UnsafeList<bool> Settled;

        // Each bucket's first entry, and each entry's cell and the next entry in its bucket.
        private UnsafeList<int> _buckets;
        private UnsafeList<int2> _entries;
        private float _bucketWidth;

        public ReverseDijkstra(int capacity, Allocator allocator)
        {
            Costs = new UnsafeList<float>(capacity, allocator);
            Settled = new UnsafeList<bool>(capacity, allocator);
            _buckets = new UnsafeList<int>(1024, allocator);
            _entries = new UnsafeList<int2>(capacity, allocator);
            _bucketWidth = 1f;
        }

        public void Dispose()
        {
            Costs.Dispose();
            Settled.Dispose();
            _buckets.Dispose();
            _entries.Dispose();
        }

        /// <summary>Starts over on <paramref name="count"/> unreached cells whose every step costs at least <paramref name="cheapestStep"/>.</summary>
        public void Reset(int count, float cheapestStep)
        {
            Costs.Resize(count);
            Settled.Resize(count);
            _buckets.Clear();
            _entries.Clear();
            _bucketWidth = cheapestStep * 0.5f;
            for (var i = 0; i < count; i++)
            {
                Costs[i] = float.PositiveInfinity;
                Settled[i] = false;
            }
        }

        /// <summary>Starts the search from a goal cell, at no cost; a closed one keeps the cost but settles nothing.</summary>
        public void Seed(int index)
        {
            Costs[index] = 0f;
            Push(index, 0f);
        }

        /// <summary>Gives a cell a cost to the goal if that is cheaper than the one it has; true when it was.</summary>
        public bool Offer(int index, float cost)
        {
            if (Settled[index] || cost >= Costs[index])
            {
                return false;
            }

            Costs[index] = cost;
            Push(index, cost);
            return true;
        }

        private void Push(int index, float cost)
        {
            var bucket = (int)(cost / _bucketWidth);
            while (_buckets.Length <= bucket)
            {
                _buckets.Add(-1);
            }

            _entries.Add(new int2(index, _buckets[bucket]));
            _buckets[bucket] = _entries.Length - 1;
        }

        /// <summary>Settles the offered cells cheapest first, each offering its neighbours; returns how many.</summary>
        public int Settle<T>(ref T graph) where T : struct, IReverseGraph
        {
            var count = 0;
            for (var bucket = 0; bucket < _buckets.Length; bucket++)
            {
                for (var entry = _buckets[bucket]; entry >= 0; entry = _entries[entry].y)
                {
                    var index = _entries[entry].x;
                    if (Settled[index])
                    {
                        continue;
                    }

                    Settled[index] = true;
                    count++;
                    graph.Relax(index, ref this);
                }
            }

            return count;
        }
    }
}
