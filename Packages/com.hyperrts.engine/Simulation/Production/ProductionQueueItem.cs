using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Paid-for units waiting to be trained; the first element is in progress.</summary>
    [InternalBufferCapacity(5)]
    public struct ProductionQueueItem : IPrefabReference
    {
        /// <summary>Not replicated; clients resolve it from <see cref="TypeId"/>.</summary>
        [GhostField(SendData = false)] public Entity Prefab;

        /// <summary>The prefab's <c>EntityInfo.TypeId</c>.</summary>
        [GhostField] public int TypeId;

        /// <summary>Refunds a whole queue whose producer is lost (sold or captured) to whoever paid.</summary>
        public static void RefundAll(DynamicBuffer<ProductionQueueItem> queue, Stockpile stock,
            in BufferLookup<ResourceCost> costs)
        {
            foreach (var item in queue)
            {
                stock.Refund(item.Prefab, costs);
            }
        }

        int IPrefabReference.TypeId => TypeId;

        Entity IPrefabReference.Prefab
        {
            get => Prefab;
            set => Prefab = value;
        }
    }
}
