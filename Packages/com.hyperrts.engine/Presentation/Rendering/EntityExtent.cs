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
            if (entityManager.HasBuffer<LinkedEntityGroup>(entity))
            {
                foreach (var part in entityManager.GetBuffer<LinkedEntityGroup>(entity, true))
                {
                    top = math.max(top, BoundsTop(entityManager, part.Value));
                }
            }
            else
            {
                top = BoundsTop(entityManager, entity);
            }

            return top > float.MinValue ? top : position.y + radius * 2f;
        }

        private static float BoundsTop(EntityManager entityManager, Entity entity) =>
            entityManager.HasComponent<WorldRenderBounds>(entity)
                ? entityManager.GetComponentData<WorldRenderBounds>(entity).Value.Max.y
                : float.MinValue;
    }
}
