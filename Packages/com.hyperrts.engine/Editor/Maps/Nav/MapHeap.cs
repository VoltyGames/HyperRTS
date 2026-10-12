using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Nav
{
    /// <summary>Binary min-heap of (cost, cell index), ordered by cost then index so ties pop the lowest cell first.</summary>
    internal sealed class MapHeap
    {
        private readonly List<(float Cost, int Index)> _items = new();

        public int Count => _items.Count;

        public void Push(float cost, int index)
        {
            _items.Add((cost, index));
            var child = _items.Count - 1;
            while (child > 0)
            {
                var parent = (child - 1) / 2;
                if (!Less(child, parent))
                {
                    break;
                }

                (_items[parent], _items[child]) = (_items[child], _items[parent]);
                child = parent;
            }
        }

        public (float Cost, int Index) Pop()
        {
            var top = _items[0];
            var last = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            if (_items.Count > 0)
            {
                _items[0] = last;
                SiftDown();
            }

            return top;
        }

        private void SiftDown()
        {
            var parent = 0;
            while (true)
            {
                var left = parent * 2 + 1;
                var smallest = parent;
                smallest = left < _items.Count && Less(left, smallest) ? left : smallest;
                var right = left + 1;
                smallest = right < _items.Count && Less(right, smallest) ? right : smallest;
                if (smallest == parent)
                {
                    return;
                }

                (_items[parent], _items[smallest]) = (_items[smallest], _items[parent]);
                parent = smallest;
            }
        }

        private bool Less(int a, int b)
        {
            var (costA, indexA) = _items[a];
            var (costB, indexB) = _items[b];
            if (costA != costB)
            {
                return costA < costB;
            }

            return indexA < indexB;
        }
    }
}
