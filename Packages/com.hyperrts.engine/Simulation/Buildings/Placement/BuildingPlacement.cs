using Unity.Entities;

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
    }
}
