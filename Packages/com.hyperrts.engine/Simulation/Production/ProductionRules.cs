using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Resources;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Option-list and refund rules shared by the build, production, sell and capture behaviours.</summary>
    public static class ProductionRules
    {
        /// <summary>Whether a finished producer of <paramref name="owner"/> offers <paramref name="prefab"/> and has queue room.</summary>
        public static bool CanQueue(EntityManager entityManager, Entity producer, byte owner, Entity prefab)
        {
            if (!entityManager.HasComponent<Producer>(producer) || entityManager.GetComponentData<Owner>(producer).Value != owner)
            {
                return false;
            }

            if (entityManager.HasEnabled<ConstructionProgress>(producer))
            {
                return false;
            }

            var hasRoom = entityManager.GetBuffer<ProductionQueueItem>(producer, true).Length
                < entityManager.GetComponentData<Producer>(producer).QueueLimit;
            return hasRoom && HasOption(entityManager.GetBuffer<ProductionOption>(producer, true), prefab);
        }

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
