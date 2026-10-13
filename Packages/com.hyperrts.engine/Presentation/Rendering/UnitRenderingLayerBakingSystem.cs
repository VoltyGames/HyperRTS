using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Puts every rendered part of a unit on <see cref="UnitRenderingLayer"/> at bake time, so unit prefabs need no
    /// authored rendering layer for <see cref="UnitSilhouetteFeature"/> to find them.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct UnitRenderingLayerBakingSystem : ISystem
    {
        private EntityQuery _rendered;

        public void OnCreate(ref SystemState state)
        {
            _rendered = new EntityQueryBuilder(Allocator.Temp).WithAll<RenderFilterSettings>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var parents = SystemAPI.GetComponentLookup<Parent>(true);
            var units = SystemAPI.GetComponentLookup<UnitTag>(true);
            var em = state.EntityManager;
            var parts = new NativeList<Entity>(Allocator.Temp);
            using var rendered = _rendered.ToEntityArray(Allocator.Temp);
            foreach (var entity in rendered)
            {
                var onLayer = (em.GetSharedComponent<RenderFilterSettings>(entity).RenderingLayerMask & UnitRenderingLayer.Mask) != 0;
                if (!onLayer && IsUnitPart(entity, in parents, in units))
                {
                    parts.Add(entity);
                }
            }

            // Setting a shared component moves the entity, so collect first.
            foreach (var part in parts)
            {
                var settings = em.GetSharedComponent<RenderFilterSettings>(part);
                settings.RenderingLayerMask |= UnitRenderingLayer.Mask;
                em.SetSharedComponent(part, settings);
            }
        }

        private static bool IsUnitPart(Entity entity, in ComponentLookup<Parent> parents, in ComponentLookup<UnitTag> units)
        {
            while (true)
            {
                if (units.HasComponent(entity))
                {
                    return true;
                }

                if (!parents.TryGetComponent(entity, out var parent))
                {
                    return false;
                }

                entity = parent.Value;
            }
        }
    }
}
