using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>Whether a player may still queue an upgrade: each is researched once. Shared by orders, AI and HUD.</summary>
    public static class UpgradeRules
    {
        /// <summary>Every production queue with its owner, for <see cref="CanQueue"/>.</summary>
        public static EntityQueryBuilder QueueQuery(Allocator allocator) =>
            new EntityQueryBuilder(allocator).WithAll<ProductionQueueItem, Faction>();

        /// <summary>False for an upgrade <paramref name="faction"/> has researched or already queued anywhere.</summary>
        public static bool CanQueue(EntityManager entityManager, EntityQuery queues,
            DynamicBuffer<ResearchedUpgrade> researched, byte faction, Entity prefab)
        {
            if (!entityManager.HasComponent<Upgrade>(prefab))
            {
                return true;
            }

            if (IsResearched(researched, prefab))
            {
                return false;
            }

            foreach (var producer in queues.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.GetComponentData<Faction>(producer).Value == faction &&
                    IsQueued(entityManager.GetBuffer<ProductionQueueItem>(producer, true), prefab))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsResearched(DynamicBuffer<ResearchedUpgrade> researched, Entity upgrade)
        {
            foreach (var item in researched)
            {
                if (item.Upgrade == upgrade)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsQueued(DynamicBuffer<ProductionQueueItem> queue, Entity upgrade)
        {
            foreach (var item in queue)
            {
                if (item.Prefab == upgrade)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
