using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering.Silhouettes
{
    /// <summary>
    /// Puts every rendered part of a unit on <see cref="UnitRenderingLayer"/> at bake time, so prefabs
    /// need no authored rendering layer for the unit silhouettes to find them.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct RenderingLayerBakingSystem : ISystem
    {
        private EntityQuery _rendered;

        public void OnCreate(ref SystemState state)
        {
            _rendered = new EntityQueryBuilder(Allocator.Temp).WithAll<RenderFilterSettings>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var roots = new UnitPartCheck
            {
                Parents = SystemAPI.GetComponentLookup<Parent>(true),
                Units = SystemAPI.GetComponentLookup<UnitTag>(true),
            };
            var entityManager = state.EntityManager;
            var parts = new NativeList<Entity>(Allocator.Temp);
            using var rendered = _rendered.ToEntityArray(Allocator.Temp);
            foreach (var entity in rendered)
            {
                var onLayer = (entityManager.GetSharedComponent<RenderFilterSettings>(entity).RenderingLayerMask & UnitRenderingLayer.Mask) != 0;
                if (!onLayer && roots.IsUnitPart(entity))
                {
                    parts.Add(entity);
                }
            }

            // Setting a shared component moves the entity, so collect first.
            foreach (var part in parts)
            {
                var settings = entityManager.GetSharedComponent<RenderFilterSettings>(part);
                settings.RenderingLayerMask |= UnitRenderingLayer.Mask;
                entityManager.SetSharedComponent(part, settings);
            }
        }
    }
}
