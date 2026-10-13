using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

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
                top = TreeTop(entityManager, entity);
            }

            return top > float.MinValue ? top : position.y + radius * 2f;
        }

        // SubScene objects (map buildings) have no LinkedEntityGroup, so walk their transform children like RenderHierarchy.
        private static float TreeTop(EntityManager entityManager, Entity entity)
        {
            var top = BoundsTop(entityManager, entity);
            if (!entityManager.HasBuffer<Child>(entity))
            {
                return top;
            }

            foreach (var child in entityManager.GetBuffer<Child>(entity, true))
            {
                top = math.max(top, TreeTop(entityManager, child.Value));
            }

            return top;
        }

        private static float BoundsTop(EntityManager entityManager, Entity entity) =>
            entityManager.HasComponent<WorldRenderBounds>(entity)
                ? entityManager.GetComponentData<WorldRenderBounds>(entity).Value.Max.y
                : float.MinValue;
    }
}
