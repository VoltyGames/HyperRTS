using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>One radius per entity for edge-to-edge distances: spatial queries, weapon ranges, reach and overlays.</summary>
    public static class EntityRadius
    {
        /// <summary>For entities with neither a <see cref="NavAgent"/> nor a <see cref="NavObstacle"/>.</summary>
        public const float Default = 0.5f;

        public static float Of(Entity entity, in ComponentLookup<NavAgent> agents,
            in ComponentLookup<NavObstacle> obstacles)
        {
            if (agents.TryGetComponent(entity, out var agent))
            {
                return agent.Radius;
            }

            return obstacles.TryGetComponent(entity, out var obstacle) ? Of(obstacle) : Default;
        }

        public static float Of(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<NavAgent>(entity))
            {
                return entityManager.GetComponentData<NavAgent>(entity).Radius;
            }

            return entityManager.HasComponent<NavObstacle>(entity)
                ? Of(entityManager.GetComponentData<NavObstacle>(entity))
                : Default;
        }

        /// <summary>Half the longest side, so the circle spans the box's width.</summary>
        public static float Of(in NavObstacle obstacle) => math.cmax(obstacle.Size) * 0.5f;

        /// <summary>Gap between two footprints on the XZ plane; weapon ranges are measured edge to edge.</summary>
        public static float EdgeDistance(float3 a, float radiusA, float3 b, float radiusB) =>
            math.max(0f, math.distance(a.xz, b.xz) - radiusA - radiusB);
    }
}
