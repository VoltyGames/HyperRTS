using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Approximate rendered height of a unit or building, for placing overlays above it.</summary>
    public static class EntityExtent
    {
        /// <summary>
        /// Top of the highest rendered part (models are often children of a mesh-less root), or a guess from the
        /// radius when nothing has bounds yet.
        /// </summary>
        public static float Top(EntityManager entityManager, Entity entity, float3 position, float radius)
        {
            var top = float.MinValue;
            using var parts = new NativeList<Entity>(Allocator.Temp);
            RenderHierarchy.Collect(entityManager, entity, parts);
            foreach (var part in parts)
            {
                top = math.max(top, BoundsTop(entityManager, part));
            }

            return top > float.MinValue ? top : position.y + radius * 2f;
        }

        private static float BoundsTop(EntityManager entityManager, Entity entity) =>
            entityManager.HasComponent<WorldRenderBounds>(entity)
                ? entityManager.GetComponentData<WorldRenderBounds>(entity).Value.Max.y
                : float.MinValue;
    }
}
