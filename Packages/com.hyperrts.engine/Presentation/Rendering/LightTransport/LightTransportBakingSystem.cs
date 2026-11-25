using HyperRTS.Presentation.Rendering.Silhouettes;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering.LightTransport
{
    /// <summary>
    /// Puts every rendered entity a scene places, except unit parts, on <see cref="LightTransportLayer"/>. Nothing in
    /// light transport may change mid-match, so spawned prefabs and units stay off it.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct LightTransportBakingSystem : ISystem
    {
        private EntityQuery _placed;

        public void OnCreate(ref SystemState state)
        {
            _placed = new EntityQueryBuilder(Allocator.Temp).WithAll<RenderFilterSettings>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var units = new UnitPartCheck
            {
                Parents = SystemAPI.GetComponentLookup<Parent>(true),
                Units = SystemAPI.GetComponentLookup<UnitTag>(true),
            };
            var entityManager = state.EntityManager;
            var scenery = new NativeList<Entity>(Allocator.Temp);
            using var rendered = _placed.ToEntityArray(Allocator.Temp);
            foreach (var entity in rendered)
            {
                var onLayer = (entityManager.GetSharedComponent<RenderFilterSettings>(entity).RenderingLayerMask & LightTransportLayer.Mask) != 0;
                if (!onLayer && !units.IsUnitPart(entity))
                {
                    scenery.Add(entity);
                }
            }

            // Setting a shared component moves the entity, so collect first.
            foreach (var entity in scenery)
            {
                var settings = entityManager.GetSharedComponent<RenderFilterSettings>(entity);
                settings.RenderingLayerMask |= LightTransportLayer.Mask;
                entityManager.SetSharedComponent(entity, settings);
            }
        }
    }
}
