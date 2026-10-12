using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>What one player's purchases are checked against, snapshotted once for many checks.</summary>
    public struct Buyer
    {
        public byte Faction;
        public CompletedBuildings Buildings;
        public DynamicBuffer<CompletedResearch> Research;

        /// <summary>Everything the player has queued anywhere; only read when the prefab is research.</summary>
        public NativeHashSet<Entity> QueuedResearch;

        /// <summary>How many of each build-limited type the player has, standing or queued.</summary>
        public BuildCounts Counts;

        /// <summary>
        /// Snapshots <paramref name="faction"/> as a buyer for this frame, collecting what it has queued at every
        /// producer in <paramref name="queues"/> (<see cref="ResearchRules.QueueQuery"/>) and its build-limited
        /// entities in <paramref name="limited"/> (<see cref="BuildCounts.Query"/>).
        /// </summary>
        public static Buyer Snapshot(EntityManager entityManager, EntityQuery queues, EntityQuery limited,
            byte faction, CompletedBuildings buildings, DynamicBuffer<CompletedResearch> research)
        {
            var buyer = new Buyer
            {
                Faction = faction,
                Buildings = buildings,
                Research = research,
                QueuedResearch = new NativeHashSet<Entity>(8, Allocator.Temp),
                Counts = BuildCounts.Snapshot(limited, faction),
            };

            foreach (var producer in queues.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.GetComponentData<Common.Faction>(producer).Value != faction)
                {
                    continue;
                }

                foreach (var item in entityManager.GetBuffer<ProductionQueueItem>(producer, true))
                {
                    buyer.AddPurchase(entityManager, item.Prefab);
                }
            }

            return buyer;
        }

        /// <summary>Counts a purchase made this frame, so later checks this frame see it.</summary>
        public void AddPurchase(EntityManager entityManager, Entity prefab)
        {
            QueuedResearch.Add(prefab);
            Counts.Add(entityManager, prefab);
        }

        /// <summary>Uncounts a cancelled queue item, so it can be bought again this frame.</summary>
        public void RemovePurchase(EntityManager entityManager, Entity prefab)
        {
            QueuedResearch.Remove(prefab);
            Counts.Remove(entityManager, prefab);
        }
    }
}
