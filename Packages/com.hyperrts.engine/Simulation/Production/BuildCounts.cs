using System;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>One player's count of each <see cref="BuildLimit"/> type, standing or queued, for purchase checks.</summary>
    public struct BuildCounts
    {
        private NativeHashMap<int, int> _counts;

        /// <summary>Living instances of limited types; instances keep the prefab's <see cref="BuildLimit"/>.</summary>
        public static EntityQueryBuilder Query(Allocator allocator) =>
            new EntityQueryBuilder(allocator).WithAll<BuildLimit, EntityInfo, Faction>().WithNone<Dead>();

        /// <summary>Counts <paramref name="faction"/>'s instances in <paramref name="limited"/> and its queued items.</summary>
        public static BuildCounts Snapshot(EntityManager entityManager, EntityQuery limited, EntityQuery queues,
            byte faction)
        {
            var counts = new BuildCounts { _counts = new NativeHashMap<int, int>(4, Allocator.Temp) };
            var infos = limited.ToComponentDataArray<EntityInfo>(Allocator.Temp);
            var owners = limited.ToComponentDataArray<Faction>(Allocator.Temp);
            for (var i = 0; i < infos.Length; i++)
            {
                if (owners[i].Value == faction)
                {
                    counts.Change(infos[i].TypeId, 1);
                }
            }

            foreach (var producer in queues.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.GetComponentData<Faction>(producer).Value != faction)
                {
                    continue;
                }

                foreach (var item in entityManager.GetBuffer<ProductionQueueItem>(producer, true))
                {
                    counts.Add(entityManager, item.Prefab);
                }
            }

            return counts;
        }

        /// <summary>True when <paramref name="prefab"/> has a <see cref="BuildLimit"/> the player already reached.</summary>
        public readonly bool AtLimit(EntityManager entityManager, Entity prefab)
        {
            if (!entityManager.HasComponent<BuildLimit>(prefab))
            {
                return false;
            }

            if (!_counts.IsCreated)
            {
                throw new InvalidOperationException("Buyer has no BuildCounts snapshot to check a BuildLimit against");
            }

            var max = entityManager.GetComponentData<BuildLimit>(prefab).Max;
            _counts.TryGetValue(EntityInfo.GetTypeId(entityManager, prefab), out var count);
            return count >= max;
        }

        /// <summary>Counts one more <paramref name="prefab"/> bought this frame, if it is limited.</summary>
        public void Add(EntityManager entityManager, Entity prefab) => Count(entityManager, prefab, 1);

        /// <summary>Counts one fewer, for a cancelled queue item.</summary>
        public void Remove(EntityManager entityManager, Entity prefab) => Count(entityManager, prefab, -1);

        private void Count(EntityManager entityManager, Entity prefab, int delta)
        {
            if (entityManager.HasComponent<BuildLimit>(prefab))
            {
                Change(EntityInfo.GetTypeId(entityManager, prefab), delta);
            }
        }

        private void Change(int typeId, int delta)
        {
            _counts.TryGetValue(typeId, out var count);
            _counts[typeId] = count + delta;
        }
    }
}
