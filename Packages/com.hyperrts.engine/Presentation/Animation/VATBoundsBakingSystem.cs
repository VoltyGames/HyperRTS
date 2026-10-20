using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Widens a vertex-animated entity's RenderBounds from the rest pose to every frame, so culling never drops a pose
    /// that reaches past it. Runs after the mesh renderer baker has written the rest-pose bounds.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct VATBoundsBakingSystem : ISystem
    {
        private EntityQuery _animated;

        public void OnCreate(ref SystemState state)
        {
            _animated = new EntityQueryBuilder(Allocator.Temp).WithAll<VATBounds>().WithAllRW<RenderBounds>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var animated = _animated.ToEntityArray(Allocator.Temp);
            foreach (var entity in animated)
            {
                em.SetComponentData(entity, new RenderBounds { Value = em.GetComponentData<VATBounds>(entity).Value });
            }
        }
    }
}
