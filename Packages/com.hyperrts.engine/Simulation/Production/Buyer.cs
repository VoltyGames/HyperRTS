using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>What one player's purchases are checked against, snapshotted once for many checks.</summary>
    public struct Buyer
    {
        public byte Owner;
        public CompletedBuildings Buildings;
        public DynamicBuffer<CompletedResearch> Research;

        /// <summary>Everything the player has queued anywhere; only read when the prefab is research.</summary>
        public NativeHashSet<Entity> QueuedResearch;

        /// <summary>How many of each build-limited type the player has, standing or queued.</summary>
        public BuildCounts Counts;

        /// <summary>
        /// Snapshots <paramref name="owner"/> as a buyer for this frame, with its queued and build-limited entities.
        /// </summary>
        public static Buyer Snapshot(EntityManager entityManager, EntityQuery queues, EntityQuery limited,
            byte owner, CompletedBuildings buildings, DynamicBuffer<CompletedResearch> research)
        {
            var buyer = new Buyer
            {
                Owner = owner,
                Buildings = buildings,
                Research = research,
                QueuedResearch = new NativeHashSet<Entity>(8, Allocator.Temp),
                Counts = BuildCounts.Snapshot(limited, owner),
            };

            foreach (var producer in queues.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.GetComponentData<Common.Players.Owner>(producer).Value != owner)
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

        /// <summary>Every production queue with its owner, for <see cref="Snapshot"/>.</summary>
        public static EntityQueryBuilder QueueQuery(Allocator allocator) =>
            new EntityQueryBuilder(allocator).WithAll<ProductionQueueItem, Owner>();

        /// <summary>False for research already completed or queued, as snapshotted; anything else can be queued.</summary>
        public readonly bool CanQueueResearch(EntityManager entityManager, Entity prefab)
        {
            if (!entityManager.HasComponent<Research>(prefab))
            {
                return true;
            }

            return !CompletedResearch.Has(Research, prefab) && !QueuedResearch.Contains(prefab);
        }

        /// <summary>What locks <paramref name="prefab"/> whatever the price, or <see cref="PurchaseRefusal.None"/>.</summary>
        public readonly PurchaseRefusal FindLock(EntityManager entityManager, Entity prefab)
        {
            if (!CanQueueResearch(entityManager, prefab))
            {
                return PurchaseRefusal.Researched;
            }

            if (Counts.AtLimit(entityManager, prefab))
            {
                return PurchaseRefusal.AtLimit;
            }

            var required = entityManager.GetBuffer<Prerequisite>(prefab, true);
            var met = Buildings.MeetsPrerequisites(required, Owner, Research);
            return met ? PurchaseRefusal.None : PurchaseRefusal.MissingPrerequisite;
        }

        public readonly bool IsUnlocked(EntityManager entityManager, Entity prefab) =>
            FindLock(entityManager, prefab) == PurchaseRefusal.None;

        /// <summary>Why a purchase is refused, for the HUD: a lock wins over the price.</summary>
        public readonly PurchaseRefusal FindRefusal(EntityManager entityManager, Entity prefab, Stockpile stock)
        {
            var refusal = FindLock(entityManager, prefab);
            if (refusal != PurchaseRefusal.None)
            {
                return refusal;
            }

            var cost = entityManager.GetBuffer<ResourceCost>(prefab, true);
            return stock.CanAfford(cost) ? PurchaseRefusal.None : PurchaseRefusal.CannotAfford;
        }

        /// <summary>Checks without paying, against a live stockpile or a copy a planner deducts from (the AI).</summary>
        public readonly bool CanBuy(EntityManager entityManager, Entity prefab, Stockpile stock)
        {
            // Cost first: it is the cheap check and the one that fails most often.
            var cost = entityManager.GetBuffer<ResourceCost>(prefab, true);
            return stock.CanAfford(cost) && IsUnlocked(entityManager, prefab);
        }

        /// <summary>Checks and pays; a refused purchase costs nothing.</summary>
        public readonly bool TryBuy(EntityManager entityManager, Entity prefab, Stockpile stock)
        {
            var cost = entityManager.GetBuffer<ResourceCost>(prefab, true);
            return IsUnlocked(entityManager, prefab) && stock.TrySpend(cost);
        }
    }
}
