using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// A player's <see cref="ResourceStock"/> with the arithmetic over it. Buffers are tiny, so linear scans beat
    /// lookups. A stockpile made from a copy can read and <see cref="Deduct"/> but not grow.
    /// </summary>
    public struct Stockpile
    {
        private NativeArray<ResourceStock> _copy;
        private DynamicBuffer<ResourceStock> _buffer;

        public Stockpile(DynamicBuffer<ResourceStock> buffer)
        {
            _buffer = buffer;
            _copy = default;
        }

        /// <summary>Reads a copy, e.g. one an AI budgets several purchases from.</summary>
        public Stockpile(NativeArray<ResourceStock> copy)
        {
            _buffer = default;
            _copy = copy;
        }

        public int Amount(UnityObjectRef<ResourceType> type)
        {
            var index = Find(type);
            return index >= 0 ? Items[index].Amount : 0;
        }

        public void Add(UnityObjectRef<ResourceType> type, int amount)
        {
            var index = Find(type);
            if (index >= 0)
            {
                _buffer.ElementAt(index).Amount += amount;
                return;
            }

            _buffer.Add(new ResourceStock { Type = type, Amount = amount });
        }

        /// <summary>
        /// Whether the stock covers <paramref name="count"/> times <paramref name="share"/> of the cost, e.g. every
        /// segment of a line. The share rounds down per resource.
        /// </summary>
        public bool CanAfford(DynamicBuffer<ResourceCost> cost, int count = 1, float share = 1f)
        {
            foreach (var item in cost)
            {
                if (Amount(item.Type) < (int)(item.Amount * share) * count)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Deducts <paramref name="share"/> of the cost, the amount <see cref="Refund"/> returns, if affordable.</summary>
        public bool TrySpend(DynamicBuffer<ResourceCost> cost, float share = 1f)
        {
            if (!CanAfford(cost, 1, share))
            {
                return false;
            }

            foreach (var item in cost)
            {
                Add(item.Type, -(int)(item.Amount * share));
            }

            return true;
        }

        /// <summary>Takes an affordable cost out of a copy, skipping resources it doesn't hold.</summary>
        public void Deduct(DynamicBuffer<ResourceCost> cost)
        {
            foreach (var item in cost)
            {
                var index = Find(item.Type);
                if (index < 0)
                {
                    continue;
                }

                var items = Items;
                var held = items[index];
                held.Amount -= item.Amount;
                items[index] = held;
            }
        }

        /// <summary>Refunds <paramref name="share"/> of the cost, rounded down per resource.</summary>
        public void Refund(DynamicBuffer<ResourceCost> cost, float share = 1f)
        {
            foreach (var item in cost)
            {
                Add(item.Type, (int)(item.Amount * share));
            }
        }

        /// <summary>Gives back the full cost of one <paramref name="prefab"/>, as a cancel does.</summary>
        public void Refund(Entity prefab, in BufferLookup<ResourceCost> costs)
        {
            if (costs.TryGetBuffer(prefab, out var cost))
            {
                Refund(cost);
            }
        }

        /// <summary>Read afresh, because adding a type can move the buffer.</summary>
        private readonly NativeArray<ResourceStock> Items => _buffer.IsCreated ? _buffer.AsNativeArray() : _copy;

        private readonly int Find(UnityObjectRef<ResourceType> type)
        {
            var items = Items;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].Type.Equals(type))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
