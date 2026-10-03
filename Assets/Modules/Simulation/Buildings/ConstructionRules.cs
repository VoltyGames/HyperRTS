using HyperRTS.Simulation.Match;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Construction-site predicates shared by orders, construction, production and the HUD.</summary>
    public static class ConstructionRules
    {
        public static bool IsUnderConstruction(in ComponentLookup<ConstructionProgress> sites, Entity entity) =>
            sites.HasComponent(entity) && sites.IsComponentEnabled(entity);

        public static bool IsUnderConstruction(EntityManager entityManager, Entity entity) =>
            entityManager.HasComponent<ConstructionProgress>(entity) &&
            entityManager.IsComponentEnabled<ConstructionProgress>(entity);

        /// <summary>An unfinished site owned by a faction allied with <paramref name="faction"/>.</summary>
        public static bool IsAlliedSite(in ComponentLookup<ConstructionProgress> sites,
            in ComponentLookup<Faction> factions, in FactionRelations relations, Entity site, byte faction) =>
            IsUnderConstruction(sites, site) && factions.TryGetComponent(site, out var owner) &&
            relations.IsAllied(faction, owner.Value);
    }
}
