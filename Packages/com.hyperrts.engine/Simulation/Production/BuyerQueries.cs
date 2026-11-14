using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>The queries a <see cref="Buyer"/> snapshot reads, made once by whatever checks purchases.</summary>
    public struct BuyerQueries
    {
        private EntityQuery _completed;
        private EntityQuery _queues;
        private EntityQuery _limited;

        public BuyerQueries(ref SystemState state)
        {
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _queues = QueueQuery().Build(ref state);
            _limited = BuildCounts.Query(Allocator.Temp).Build(ref state);
        }

        public BuyerQueries(EntityManager entityManager)
        {
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(entityManager);
            _queues = QueueQuery().Build(entityManager);
            _limited = BuildCounts.Query(Allocator.Temp).Build(entityManager);
        }

        /// <summary>
        /// Snapshots <paramref name="player"/> as a buyer for this frame, with its queued items, build-limited entities
        /// and finished research.
        /// </summary>
        public readonly Buyer Snapshot(EntityManager entityManager, Entity player)
        {
            var owner = entityManager.GetComponentData<Player>(player).Owner;
            var buyer = new Buyer
            {
                Owner = owner,
                Buildings = new CompletedBuildings(_completed, Allocator.Temp),
                Research = entityManager.GetBuffer<CompletedResearch>(player, true),
                Counts = BuildCounts.Snapshot(_limited, owner, false),
            };
            buyer.Counts.AddResearch(buyer.Research);
            foreach (var prefab in FindQueued(entityManager, owner))
            {
                buyer.AddPurchase(entityManager, prefab);
            }

            return buyer;
        }

        /// <summary>
        /// Per type, everything <paramref name="player"/> has: its instances in <paramref name="owned"/>, its queued
        /// items and its finished research.
        /// </summary>
        public readonly BuildCounts CountEveryType(EntityManager entityManager, Entity player, EntityQuery owned)
        {
            var owner = entityManager.GetComponentData<Player>(player).Owner;
            var counts = BuildCounts.Snapshot(owned, owner, true);
            foreach (var prefab in FindQueued(entityManager, owner))
            {
                counts.Add(entityManager, prefab);
            }

            counts.AddResearch(entityManager.GetBuffer<CompletedResearch>(player, true));
            return counts;
        }

        /// <summary>Every item <paramref name="owner"/> has queued at any producer.</summary>
        private readonly NativeList<Entity> FindQueued(EntityManager entityManager, byte owner)
        {
            var queued = new NativeList<Entity>(Allocator.Temp);
            foreach (var producer in _queues.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.GetComponentData<Owner>(producer).Value != owner)
                {
                    continue;
                }

                foreach (var item in entityManager.GetBuffer<ProductionQueueItem>(producer, true))
                {
                    queued.Add(item.Prefab);
                }
            }

            return queued;
        }

        private static EntityQueryBuilder QueueQuery() =>
            new EntityQueryBuilder(Allocator.Temp).WithAll<ProductionQueueItem, Owner>();
    }
}
