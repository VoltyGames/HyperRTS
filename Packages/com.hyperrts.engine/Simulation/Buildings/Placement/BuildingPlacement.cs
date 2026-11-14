using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings.Placement
{
    /// <summary>Where a building may be placed; buildings without it go on land.</summary>
    public struct BuildingPlacement : IComponentData
    {
        public PlacementSurface Surface;

        public static PlacementSurface GetSurface(EntityManager entityManager, Entity prefab) =>
            entityManager.HasComponent<BuildingPlacement>(prefab)
                ? entityManager.GetComponentData<BuildingPlacement>(prefab).Surface
                : PlacementSurface.Land;

        /// <summary>A building prefab carrying everything placement reads, so a builder places it rather than a queue.</summary>
        public static bool IsPlaceable(EntityManager entityManager, Entity prefab)
        {
            if (!entityManager.HasComponent<BuildingTag>(prefab) || !entityManager.HasComponent<ConstructionProgress>(prefab))
            {
                return false;
            }

            return entityManager.HasComponent<NavObstacle>(prefab) && entityManager.HasComponent<LocalTransform>(prefab);
        }
    }
}
