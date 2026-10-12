using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Whether a player may still queue a <see cref="Research"/>: each is researched once. Shared by orders, AI and HUD.</summary>
    public static class ResearchRules
    {
        /// <summary>Every production queue with its owner, for <see cref="Buyer.Snapshot"/>.</summary>
        public static EntityQueryBuilder QueueQuery(Allocator allocator) =>
            new EntityQueryBuilder(allocator).WithAll<ProductionQueueItem, Faction>();

        /// <summary>False for research already in <paramref name="completed"/> or in <paramref name="queued"/>, as snapshotted by <see cref="Buyer.Snapshot"/>.</summary>
        public static bool CanQueue(EntityManager entityManager, DynamicBuffer<CompletedResearch> completed,
            NativeHashSet<Entity> queued, Entity prefab)
        {
            if (!entityManager.HasComponent<Research>(prefab))
            {
                return true;
            }

            return !IsDone(completed, prefab) && !queued.Contains(prefab);
        }

        public static bool IsDone(DynamicBuffer<CompletedResearch> completed, Entity prefab)
        {
            foreach (var item in completed)
            {
                if (item.Prefab == prefab)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsDone(DynamicBuffer<CompletedResearch> completed, int typeId)
        {
            foreach (var item in completed)
            {
                if (item.TypeId == typeId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
