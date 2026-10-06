using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>Whether a player may still queue an upgrade: each is researched once. Shared by orders, AI and HUD.</summary>
    public static class UpgradeRules
    {
        /// <summary>Every production queue with its owner, for <see cref="CollectQueued"/>.</summary>
        public static EntityQueryBuilder QueueQuery(Allocator allocator) =>
            new EntityQueryBuilder(allocator).WithAll<ProductionQueueItem, Faction>();

        /// <summary>Adds every prefab <paramref name="faction"/> has queued at any producer to <paramref name="queued"/>.</summary>
        public static void CollectQueued(EntityManager entityManager, EntityQuery queues, byte faction,
            NativeHashSet<Entity> queued)
        {
            foreach (var producer in queues.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.GetComponentData<Faction>(producer).Value != faction)
                {
                    continue;
                }

                foreach (var item in entityManager.GetBuffer<ProductionQueueItem>(producer, true))
                {
                    queued.Add(item.Prefab);
                }
            }
        }

        /// <summary>False for an upgrade <paramref name="faction"/> has researched or already queued anywhere.</summary>
        public static bool CanQueue(EntityManager entityManager, EntityQuery queues,
            DynamicBuffer<ResearchedUpgrade> researched, byte faction, Entity prefab)
        {
            if (!entityManager.HasComponent<Upgrade>(prefab))
            {
                return true;
            }

            var queued = new NativeHashSet<Entity>(8, Allocator.Temp);
            CollectQueued(entityManager, queues, faction, queued);
            return CanQueue(entityManager, researched, queued, prefab);
        }

        /// <summary>As above, against a <see cref="CollectQueued"/> snapshot shared by many checks.</summary>
        public static bool CanQueue(EntityManager entityManager, DynamicBuffer<ResearchedUpgrade> researched,
            NativeHashSet<Entity> queued, Entity prefab)
        {
            if (!entityManager.HasComponent<Upgrade>(prefab))
            {
                return true;
            }

            return !IsResearched(researched, prefab) && !queued.Contains(prefab);
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
    }
}
