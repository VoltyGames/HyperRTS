using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
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

        /// <summary>An unfinished site owned by a faction allied with <paramref name="faction"/>.</summary>
        public static bool IsAlliedSite(in ComponentLookup<ConstructionProgress> sites,
            in ComponentLookup<Faction> factions, in FactionRelations relations, Entity site, byte faction)
        {
            if (!sites.HasEnabled(site))
            {
                return false;
            }

            return factions.TryGetComponent(site, out var owner) && relations.IsAllied(faction, owner.Value);
        }

        /// <summary>
        /// A damaged, living building to repair: a finished one allied with <paramref name="faction"/>, or a
        /// <see cref="Repairable"/> one, which belongs to nobody and anyone's builders mend.
        /// </summary>
        public static bool NeedsRepair(in ComponentLookup<Health> health, in ComponentLookup<ConstructionProgress> sites,
            in ComponentLookup<Faction> factions, in ComponentLookup<Repairable> repairables,
            in FactionRelations relations, Entity target, byte faction)
        {
            if (!Health.IsAlive(health, target) || health[target].Current >= health[target].Max)
            {
                return false;
            }

            if (repairables.HasComponent(target))
            {
                return true;
            }

            // Sites are built, not repaired.
            var finished = sites.HasComponent(target) && !sites.IsComponentEnabled(target);
            return finished && factions.TryGetComponent(target, out var owner) && relations.IsAllied(faction, owner.Value);
        }
    }
}
