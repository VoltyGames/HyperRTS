using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// What one player's purchases are checked against, snapshotted once for many checks by
    /// <see cref="BuyerQueries.Snapshot"/>.
    /// </summary>
    public struct Buyer
    {
        public byte Owner;
        public CompletedBuildings Buildings;
        public DynamicBuffer<CompletedResearch> Research;

        /// <summary>Everything the player has queued anywhere; only read when the prefab is research.</summary>
        public NativeHashSet<Entity> QueuedResearch;

        /// <summary>How many of each build-limited type the player has, standing or queued.</summary>
        public BuildCounts Counts;

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

        /// <summary>The prefabs of <paramref name="prefab"/>'s unmet prerequisites that have one.</summary>
        public readonly NativeList<Entity> FindMissingPrerequisites(EntityManager entityManager, Entity prefab,
            in PrefabRegistry registry)
        {
            var missing = new NativeList<Entity>(Allocator.Temp);
            foreach (var prerequisite in entityManager.GetBuffer<Prerequisite>(prefab, true))
            {
                var required = registry.Find(prerequisite.TypeId);
                var unmet = !Buildings.IsMet(Owner, prerequisite.TypeId, Research);
                if (unmet && required != Entity.Null)
                {
                    missing.Add(required);
                }
            }

            return missing;
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
