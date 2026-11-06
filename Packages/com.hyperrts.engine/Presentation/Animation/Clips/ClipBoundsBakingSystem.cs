using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Animation.Clips
{
    /// <summary>
    /// Widens an animated entity's RenderBounds from the rest pose to every row, so culling never drops a pose that
    /// reaches past it. Runs after the mesh renderer baker has written the rest-pose bounds.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct ClipBoundsBakingSystem : ISystem
    {
        private EntityQuery _animated;

        public void OnCreate(ref SystemState state)
        {
            _animated = new EntityQueryBuilder(Allocator.Temp).WithAll<ClipBounds>().WithAllRW<RenderBounds>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var animated = _animated.ToEntityArray(Allocator.Temp);
            foreach (var entity in animated)
            {
                em.SetComponentData(entity, new RenderBounds { Value = em.GetComponentData<ClipBounds>(entity).Value });
            }
        }
    }
}
