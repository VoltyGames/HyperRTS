using Unity.Collections;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Binary min-heap of grid cells keyed by A* cost. Stale duplicates are skipped by the caller.</summary>
    internal struct OpenSet
    {
        private struct Node
        {
            public int Cell;
            public float Cost;
        }

        private NativeList<Node> _heap;

        public OpenSet(int capacity, Allocator allocator) => _heap = new NativeList<Node>(capacity, allocator);

        public void Dispose() => _heap.Dispose();

        public void Push(int cell, float cost)
        {
            _heap.Add(new Node { Cell = cell, Cost = cost });
            var i = _heap.Length - 1;
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (_heap[parent].Cost <= _heap[i].Cost)
                {
                    break;
                }

                Swap(i, parent);
                i = parent;
            }
        }

        public bool TryPop(out int cell)
        {
            if (_heap.Length == 0)
            {
                cell = -1;
                return false;
            }

            cell = _heap[0].Cell;
            _heap[0] = _heap[_heap.Length - 1];
            _heap.RemoveAt(_heap.Length - 1);
            SiftDown(0);
            return true;
        }

        private void SiftDown(int i)
        {
            while (true)
            {
                var smallest = i;
                var left = i * 2 + 1;
                var right = left + 1;
                if (left < _heap.Length && _heap[left].Cost < _heap[smallest].Cost)
                {
                    smallest = left;
                }

                if (right < _heap.Length && _heap[right].Cost < _heap[smallest].Cost)
                {
                    smallest = right;
                }

                if (smallest == i)
                {
                    return;
                }

                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b) => (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
    }
}
