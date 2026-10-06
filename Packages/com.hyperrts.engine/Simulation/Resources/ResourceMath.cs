using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Stockpile arithmetic. Buffers are tiny, so linear scans beat lookups.</summary>
    public static class ResourceMath
    {
        public static int GetAmount(DynamicBuffer<ResourceStock> stock, UnityObjectRef<ResourceType> type) =>
            GetAmount(stock.AsNativeArray(), type);

        public static int GetAmount(NativeArray<ResourceStock> stock, UnityObjectRef<ResourceType> type)
        {
            var index = IndexOf(stock, type);
            return index >= 0 ? stock[index].Amount : 0;
        }

        public static void Add(DynamicBuffer<ResourceStock> stock, UnityObjectRef<ResourceType> type, int amount)
        {
            var index = IndexOf(stock.AsNativeArray(), type);
            if (index >= 0)
            {
                stock.ElementAt(index).Amount += amount;
                return;
            }

            stock.Add(new ResourceStock { Type = type, Amount = amount });
        }

        public static bool CanAfford(DynamicBuffer<ResourceStock> stock, DynamicBuffer<ResourceCost> cost) =>
            CanAfford(stock.AsNativeArray(), cost);

        public static bool CanAfford(NativeArray<ResourceStock> stock, DynamicBuffer<ResourceCost> cost)
        {
            foreach (var item in cost)
            {
                if (GetAmount(stock, item.Type) < item.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Takes an affordable cost out of a stockpile copy, e.g. one an AI budgets several purchases from.</summary>
        public static void Deduct(NativeArray<ResourceStock> stock, DynamicBuffer<ResourceCost> cost)
        {
            foreach (var item in cost)
            {
                var index = IndexOf(stock, item.Type);
                if (index < 0)
                {
                    continue;
                }

                var held = stock[index];
                held.Amount -= item.Amount;
                stock[index] = held;
            }
        }

        /// <summary>Deducts the cost if affordable; otherwise leaves the stock untouched.</summary>
        public static bool TrySpend(DynamicBuffer<ResourceStock> stock, DynamicBuffer<ResourceCost> cost)
        {
            if (!CanAfford(stock, cost))
            {
                return false;
            }

            foreach (var item in cost)
            {
                Add(stock, item.Type, -item.Amount);
            }

            return true;
        }

        public static void Refund(DynamicBuffer<ResourceStock> stock, DynamicBuffer<ResourceCost> cost)
        {
            foreach (var item in cost)
            {
                Add(stock, item.Type, item.Amount);
            }
        }

        private static int IndexOf(NativeArray<ResourceStock> stock, UnityObjectRef<ResourceType> type)
        {
            for (var i = 0; i < stock.Length; i++)
            {
                if (stock[i].Type.Equals(type))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Refunds <paramref name="share"/> of the cost, rounded down per resource.</summary>
        public static void Refund(DynamicBuffer<ResourceStock> stock, DynamicBuffer<ResourceCost> cost, float share)
        {
            foreach (var item in cost)
            {
                Add(stock, item.Type, (int)(item.Amount * share));
            }
        }
    }
}
