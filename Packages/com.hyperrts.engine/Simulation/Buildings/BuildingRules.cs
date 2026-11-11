using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Transport;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Build, repair and sell target predicates shared by orders, the HUD and the behaviours.</summary>
    public static class BuildingRules
    {
        /// <summary>
        /// Whether its owner may sell it: a building they built or hold as theirs, never a
        /// <see cref="NeutralGarrison"/> they only occupy.
        /// </summary>
        public static bool IsSellable(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<BuildingTag>(entity))
            {
                return false;
            }

            return !entityManager.HasComponent<NeutralGarrison>(entity);
        }

        /// <summary>An unfinished site owned by an owner allied with <paramref name="owner"/>.</summary>
        public static bool IsAlliedSite(in ComponentLookup<ConstructionProgress> sites,
            in ComponentLookup<Owner> owners, in OwnerRelations relations, Entity site, byte owner)
        {
            if (!sites.HasEnabled(site))
            {
                return false;
            }

            return owners.TryGetComponent(site, out var siteOwner) && relations.IsAllied(owner, siteOwner.Value);
        }
    }
}
