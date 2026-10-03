using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Circle radius of an entity's footprint, shared by spatial queries, weapon ranges and overlays.</summary>
    public static class Footprint
    {
        /// <summary>For entities with neither a <see cref="NavAgent"/> nor a <see cref="NavObstacle"/>.</summary>
        public const float DefaultRadius = 0.5f;

        public static float Radius(Entity entity, in ComponentLookup<NavAgent> agents,
            in ComponentLookup<NavObstacle> obstacles)
        {
            if (agents.TryGetComponent(entity, out var agent))
            {
                return agent.Radius;
            }

            return obstacles.TryGetComponent(entity, out var obstacle) ? Radius(obstacle) : DefaultRadius;
        }

        public static float Radius(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<NavAgent>(entity))
            {
                return entityManager.GetComponentData<NavAgent>(entity).Radius;
            }

            return entityManager.HasComponent<NavObstacle>(entity)
                ? Radius(entityManager.GetComponentData<NavObstacle>(entity))
                : DefaultRadius;
        }

        /// <summary>Half the longest side, so the circle spans the box's width.</summary>
        public static float Radius(in NavObstacle obstacle) => math.cmax(obstacle.Size) * 0.5f;
    }
}
