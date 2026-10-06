using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Approximate rendered height of a unit or building, for placing overlays above it.</summary>
    public static class EntityExtent
    {
        /// <summary>Top of the rendered mesh, or a guess from the radius when the root has no mesh bounds.</summary>
        public static float Top(EntityManager entityManager, Entity entity, float3 position, float radius)
        {
            return entityManager.HasComponent<WorldRenderBounds>(entity)
                ? entityManager.GetComponentData<WorldRenderBounds>(entity).Value.Max.y
                : position.y + radius * 2f;
        }
    }
}
