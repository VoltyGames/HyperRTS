using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// The one rule for buying a unit, building or research, shared by player orders, placement, the AI and the HUD:
    /// its prerequisites are met, research is neither done nor queued, and the player can pay.
    /// </summary>
    public static class PurchaseRules
    {
        public static bool IsUnlocked(EntityManager entityManager, Entity prefab, in Buyer buyer)
        {
            if (!ResearchRules.CanQueue(entityManager, buyer.Research, buyer.QueuedResearch, prefab))
            {
                return false;
            }

            var required = entityManager.GetBuffer<Prerequisite>(prefab, true);
            return buyer.Buildings.MeetsPrerequisites(required, buyer.Faction, buyer.Research);
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
