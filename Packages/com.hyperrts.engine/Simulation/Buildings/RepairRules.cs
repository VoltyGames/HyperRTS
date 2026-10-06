using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Repair-target predicate shared by orders and the repair behaviour.</summary>
    public static class RepairRules
    {
        /// <summary>A damaged, finished, living building allied with <paramref name="faction"/>.</summary>
        public static bool NeedsRepair(in ComponentLookup<Health> health, in ComponentLookup<ConstructionProgress> sites,
            in ComponentLookup<Faction> factions, in FactionRelations relations, Entity target, byte faction) =>
            sites.HasComponent(target) && !sites.IsComponentEnabled(target) &&
            health.TryGetComponent(target, out var current) && current.Current > 0f && current.Current < current.Max &&
            factions.TryGetComponent(target, out var owner) && relations.IsAllied(faction, owner.Value);
    }
}
