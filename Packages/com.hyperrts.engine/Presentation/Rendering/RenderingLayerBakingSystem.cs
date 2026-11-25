using HyperRTS.Presentation.Rendering.LightTransport;
using HyperRTS.Presentation.Rendering.Silhouettes;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Puts every rendered part of a unit on <see cref="UnitRenderingLayer"/>, and everything else a scene places on
    /// <see cref="LightTransportLayer"/>, so prefabs need no authored layer and nothing spawned mid-match bounces light.
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
            var units = new UnitPartCheck
            {
                Parents = SystemAPI.GetComponentLookup<Parent>(true),
                Units = SystemAPI.GetComponentLookup<UnitTag>(true),
            };
            var prefabs = SystemAPI.GetComponentLookup<Prefab>(true);
            var entityManager = state.EntityManager;
            using var changes = new NativeList<LayerChange>(Allocator.Temp);
            using var rendered = _rendered.ToEntityArray(Allocator.Temp);
            foreach (var entity in rendered)
            {
                var layer = units.IsUnitPart(entity) ? UnitRenderingLayer.Mask : PlacedLayer(entity, prefabs);
                var mask = entityManager.GetSharedComponent<RenderFilterSettings>(entity).RenderingLayerMask;
                if ((mask & layer) != layer)
                {
                    changes.Add(new LayerChange { Entity = entity, Layer = layer });
                }
            }

            // Setting a shared component moves the entity, so collect first.
            foreach (var change in changes)
            {
                var settings = entityManager.GetSharedComponent<RenderFilterSettings>(change.Entity);
                settings.RenderingLayerMask |= change.Layer;
                entityManager.SetSharedComponent(change.Entity, settings);
            }
        }

        // Only what the scene places bounces light: a prefab can spawn mid-match.
        private static uint PlacedLayer(Entity entity, ComponentLookup<Prefab> prefabs) =>
            prefabs.HasComponent(entity) ? 0u : LightTransportLayer.Mask;

        private struct LayerChange
        {
            public Entity Entity;
            public uint Layer;
        }

        private struct UnitPartCheck
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
