using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// One player's count of each <see cref="BuildLimit"/> type, or of every type, standing or queued, for purchase
    /// checks and the AI's build order.
    /// </summary>
    public struct BuildCounts
    {
        private NativeHashMap<int, int> _counts;
        private bool _everyType;

        /// <summary>Living instances of limited types; instances keep the prefab's <see cref="BuildLimit"/>.</summary>
        public static EntityQueryBuilder Query(Allocator allocator) =>
            new EntityQueryBuilder(allocator).WithAll<BuildLimit, EntityInfo, Owner>().WithNone<Dead>();

        /// <summary>
        /// Counts <paramref name="owner"/>'s instances in <paramref name="instances"/>, of build-limited types only
        /// unless <paramref name="everyType"/>; <see cref="BuyerQueries"/> adds the queued items.
        /// </summary>
        public static BuildCounts Snapshot(EntityQuery instances, byte owner, bool everyType)
        {
            var counts = new BuildCounts
            {
                _counts = new NativeHashMap<int, int>(everyType ? 64 : 4, Allocator.Temp),
                _everyType = everyType,
            };
            var infos = instances.ToComponentDataArray<EntityInfo>(Allocator.Temp);
            var owners = instances.ToComponentDataArray<Owner>(Allocator.Temp);
            for (var i = 0; i < infos.Length; i++)
            {
                if (owners[i].Value == owner)
                {
                    counts.Change(infos[i].TypeId, 1);
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

            return Count(entityManager, prefab) >= entityManager.GetComponentData<BuildLimit>(prefab).Max;
        }

        /// <summary>How many of <paramref name="prefab"/>'s type the player has, standing or queued; 0 if not counted.</summary>
        public readonly int Count(EntityManager entityManager, Entity prefab)
        {
            _counts.TryGetValue(EntityInfo.GetTypeId(entityManager, prefab), out var count);
            return count;
        }

        /// <summary>Counts one more <paramref name="prefab"/> bought this frame, if its type is counted.</summary>
        public void Add(EntityManager entityManager, Entity prefab) => Count(entityManager, prefab, 1);

        /// <summary>Counts one fewer, for a cancelled queue item.</summary>
        public void Remove(EntityManager entityManager, Entity prefab) => Count(entityManager, prefab, -1);

        private void Count(EntityManager entityManager, Entity prefab, int delta)
        {
            if (_everyType || entityManager.HasComponent<BuildLimit>(prefab))
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
