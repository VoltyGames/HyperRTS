using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering
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
            var roots = new UnitRoots
            {
                Parents = SystemAPI.GetComponentLookup<Parent>(true),
                Units = SystemAPI.GetComponentLookup<UnitTag>(true),
            };
            var em = state.EntityManager;
            var parts = new NativeList<Entity>(Allocator.Temp);
            using var rendered = _rendered.ToEntityArray(Allocator.Temp);
            foreach (var entity in rendered)
            {
                var onLayer = (em.GetSharedComponent<RenderFilterSettings>(entity).RenderingLayerMask & UnitRenderingLayer.Mask) != 0;
                if (!onLayer && roots.IsUnitPart(entity))
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

        private struct UnitRoots
        {
            public ComponentLookup<Parent> Parents;
            public ComponentLookup<UnitTag> Units;

            /// <summary>Whether a unit sits at or above the entity.</summary>
            public bool IsUnitPart(Entity entity)
            {
                while (true)
                {
                    if (Units.HasComponent(entity))
                    {
                        return true;
                    }

                    if (!Parents.TryGetComponent(entity, out var parent))
                    {
                        return false;
                    }

                    entity = parent.Value;
                }
            }
        }
    }
}
