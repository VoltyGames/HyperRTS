using HyperRTS.Simulation.Resources;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Option-list and refund rules shared by the build, production, sell and capture behaviours.</summary>
    public static class ProductionRules
    {
        public static bool HasOption<T>(DynamicBuffer<T> options, Entity prefab)
            where T : unmanaged, IBufferElementData, IPrefabOption
        {
            foreach (var option in options)
            {
                if (option.Prefab == prefab)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Gives back the full cost of one queued <paramref name="prefab"/>, as a cancel does.</summary>
        public static void Refund(DynamicBuffer<ResourceStock> stock, Entity prefab, in BufferLookup<ResourceCost> costs)
        {
            if (costs.TryGetBuffer(prefab, out var cost))
            {
                ResourceMath.Refund(stock, cost);
            }
        }

        /// <summary>Refunds a whole queue whose producer is lost (sold or captured) to whoever paid.</summary>
        public static void RefundQueue(DynamicBuffer<ResourceStock> stock, DynamicBuffer<ProductionQueueItem> queue,
            in BufferLookup<ResourceCost> costs)
        {
            foreach (var item in queue)
            {
                Refund(stock, item.Prefab, costs);
            }
        }
    }
}
