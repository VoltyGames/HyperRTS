using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Puts every rendered part of a unit on <see cref="UnitRenderingLayer"/> and of a building on
    /// <see cref="BuildingRenderingLayer"/> at bake time, so prefabs need no authored rendering layer for the unit
    /// silhouettes and team outlines to find them.
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
            var roots = new RootKinds
            {
                Parents = SystemAPI.GetComponentLookup<Parent>(true),
                Units = SystemAPI.GetComponentLookup<UnitTag>(true),
                Buildings = SystemAPI.GetComponentLookup<BuildingTag>(true),
            };
            var em = state.EntityManager;
            var parts = new NativeList<(Entity Part, uint Layer)>(Allocator.Temp);
            using var rendered = _rendered.ToEntityArray(Allocator.Temp);
            foreach (var entity in rendered)
            {
                var layer = roots.Layer(entity);
                var onLayer = (em.GetSharedComponent<RenderFilterSettings>(entity).RenderingLayerMask & layer) == layer;
                if (!onLayer)
                {
                    parts.Add((entity, layer));
                }
            }

            // Setting a shared component moves the entity, so collect first.
            foreach (var (part, layer) in parts)
            {
                var settings = em.GetSharedComponent<RenderFilterSettings>(part);
                settings.RenderingLayerMask |= layer;
                em.SetSharedComponent(part, settings);
            }
        }

        private struct RootKinds
        {
            public ComponentLookup<Parent> Parents;
            public ComponentLookup<UnitTag> Units;
            public ComponentLookup<BuildingTag> Buildings;

            /// <summary>The layer of the nearest unit or building above the entity, or none for scenery.</summary>
            public uint Layer(Entity entity)
            {
                while (true)
                {
                    if (Units.HasComponent(entity))
                    {
                        return UnitRenderingLayer.Mask;
                    }

                    if (Buildings.HasComponent(entity))
                    {
                        return BuildingRenderingLayer.Mask;
                    }

                    if (!Parents.TryGetComponent(entity, out var parent))
                    {
                        return 0u;
                    }

                    entity = parent.Value;
                }
            }
        }
    }
}
