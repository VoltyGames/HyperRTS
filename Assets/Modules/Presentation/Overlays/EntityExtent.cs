using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>Approximate on-screen size of a unit or building, for sizing overlays.</summary>
    public static class EntityExtent
    {
        /// <summary>Same footprint radius the simulation uses (<see cref="Footprint"/>).</summary>
        public static float Radius(EntityManager entityManager, Entity entity) =>
            Footprint.Radius(entityManager, entity);

        /// <summary>Top of the rendered mesh, or a guess from the radius when the root has no mesh bounds.</summary>
        public static float Top(EntityManager entityManager, Entity entity, float3 position, float radius)
        {
            return entityManager.HasComponent<WorldRenderBounds>(entity)
                ? entityManager.GetComponentData<WorldRenderBounds>(entity).Value.Max.y
                : position.y + radius * 2f;
        }
    }
}
