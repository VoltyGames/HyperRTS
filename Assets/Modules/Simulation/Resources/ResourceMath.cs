using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Stockpile arithmetic. Buffers are tiny, so linear scans beat lookups.</summary>
    public static class ResourceMath
    {
        public static int GetAmount(DynamicBuffer<ResourceStock> stock, UnityObjectRef<ResourceType> type)
        {
            foreach (var item in stock)
            {
                if (item.Type.Equals(type))
                {
                    return item.Amount;
                }
            }

            return 0;
        }

        public static void Add(DynamicBuffer<ResourceStock> stock, UnityObjectRef<ResourceType> type, int amount)
        {
            for (var i = 0; i < stock.Length; i++)
            {
                if (stock[i].Type.Equals(type))
                {
                    stock.ElementAt(i).Amount += amount;
                    return;
                }
            }

            stock.Add(new ResourceStock { Type = type, Amount = amount });
        }

        public static bool CanAfford(DynamicBuffer<ResourceStock> stock, DynamicBuffer<ResourceCost> cost)
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
    }
}
