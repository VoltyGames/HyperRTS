using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// The one rule for buying a unit, building or research, shared by player orders, placement, the AI and the HUD:
    /// its prerequisites are met, research is neither done nor queued, its build limit is not reached, and the player
    /// can pay.
    /// </summary>
    public static class PurchaseRules
    {
        /// <summary>What locks <paramref name="prefab"/> whatever the price, or <see cref="PurchaseRefusal.None"/>.</summary>
        public static PurchaseRefusal FindLock(EntityManager entityManager, Entity prefab, in Buyer buyer)
        {
            if (!ResearchRules.CanQueue(entityManager, buyer.Research, buyer.QueuedResearch, prefab))
            {
                return PurchaseRefusal.Researched;
            }

            if (buyer.Counts.AtLimit(entityManager, prefab))
            {
                return PurchaseRefusal.AtLimit;
            }

            var required = entityManager.GetBuffer<Prerequisite>(prefab, true);
            var met = buyer.Buildings.MeetsPrerequisites(required, buyer.Faction, buyer.Research);
            return met ? PurchaseRefusal.None : PurchaseRefusal.MissingPrerequisite;
        }

        public static bool IsUnlocked(EntityManager entityManager, Entity prefab, in Buyer buyer) =>
            FindLock(entityManager, prefab, buyer) == PurchaseRefusal.None;

        /// <summary>Why a purchase is refused, for the HUD: a lock wins over the price.</summary>
        public static PurchaseRefusal FindRefusal(EntityManager entityManager, Entity prefab, in Buyer buyer,
            NativeArray<ResourceStock> stock)
        {
            var refusal = FindLock(entityManager, prefab, buyer);
            if (refusal != PurchaseRefusal.None)
            {
                return refusal;
            }

            var cost = entityManager.GetBuffer<ResourceCost>(prefab, true);
            return ResourceMath.CanAfford(stock, cost) ? PurchaseRefusal.None : PurchaseRefusal.CannotAfford;
        }

        /// <summary>Checks without paying, against a live stockpile or a copy a planner deducts from (the AI).</summary>
        public static bool CanBuy(EntityManager entityManager, Entity prefab, in Buyer buyer,
            NativeArray<ResourceStock> stock)
        {
            // Cost first: it is the cheap check and the one that fails most often.
            var cost = entityManager.GetBuffer<ResourceCost>(prefab, true);
            return ResourceMath.CanAfford(stock, cost) && IsUnlocked(entityManager, prefab, buyer);
        }

        /// <summary>Checks and pays; a refused purchase costs nothing.</summary>
        public static bool TryBuy(EntityManager entityManager, Entity prefab, in Buyer buyer,
            DynamicBuffer<ResourceStock> stock)
        {
            var cost = entityManager.GetBuffer<ResourceCost>(prefab, true);
            return IsUnlocked(entityManager, prefab, buyer) && ResourceMath.TrySpend(stock, cost);
        }
    }
}
