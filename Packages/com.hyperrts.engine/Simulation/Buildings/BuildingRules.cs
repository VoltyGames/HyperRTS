using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Build and repair target predicates shared by orders and the construction and repair behaviours.</summary>
    public static class BuildingRules
    {
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

        /// <summary>A damaged, finished, living building allied with <paramref name="faction"/>.</summary>
        public static bool NeedsRepair(in ComponentLookup<Health> health, in ComponentLookup<ConstructionProgress> sites,
            in ComponentLookup<Faction> factions, in FactionRelations relations, Entity target, byte faction)
        {
            // Sites are built, not repaired.
            var finished = sites.HasComponent(target) && !sites.IsComponentEnabled(target);
            if (!finished || !Health.IsAlive(health, target))
            {
                return false;
            }

            var current = health[target];
            if (current.Current >= current.Max)
            {
                return false;
            }

            return factions.TryGetComponent(target, out var owner) && relations.IsAllied(faction, owner.Value);
        }
    }
}
