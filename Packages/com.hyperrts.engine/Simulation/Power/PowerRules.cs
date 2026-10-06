using Unity.Entities;

namespace HyperRTS.Simulation.Power
{
    /// <summary>Power predicates shared by production and abilities.</summary>
    public static class PowerRules
    {
        public static bool IsUnpowered(in ComponentLookup<Unpowered> unpowered, Entity entity) =>
            unpowered.HasComponent(entity) && unpowered.IsComponentEnabled(entity);

        public static bool IsUnpowered(EntityManager entityManager, Entity entity) =>
            entityManager.HasComponent<Unpowered>(entity) && entityManager.IsComponentEnabled<Unpowered>(entity);
    }
}
