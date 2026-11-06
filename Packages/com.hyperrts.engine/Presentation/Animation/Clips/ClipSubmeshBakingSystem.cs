using HyperRTS.Presentation.Animation.Bones;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Clips
{
    /// <summary>
    /// Gives the extra material entities of a multi-material animated renderer the clips and bounds of the renderer's
    /// own entity, since Entities Graphics bakes each material as a child entity without them.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateBefore(typeof(ClipBoundsBakingSystem))]
    public partial struct ClipSubmeshBakingSystem : ISystem
    {
        private EntityQuery _meshes;

        public void OnCreate(ref SystemState state)
        {
            _meshes = new EntityQueryBuilder(Allocator.Temp).WithAll<Parent, MaterialMeshInfo>().WithNone<ClipBounds>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities |
                             EntityQueryOptions.IgnoreComponentEnabledState).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var meshes = _meshes.ToEntityArray(Allocator.Temp);
            foreach (var mesh in meshes)
            {
                var owner = em.GetComponentData<Parent>(mesh).Value;
                if (!em.HasComponent<ClipBounds>(owner))
                {
                    continue;
                }

                em.AddComponentData(mesh, em.GetComponentData<ClipBounds>(owner));
                Copy<PlayingClip>(em, owner, mesh);
                Copy<LowerClip>(em, owner, mesh);
                Copy<UpperClip>(em, owner, mesh);
                Copy<LowerFade>(em, owner, mesh);
                Copy<UpperFade>(em, owner, mesh);
            }
        }

        // An owner plays one clip or a lower and upper pair, so it has only some of these.
        private static void Copy<T>(EntityManager em, Entity owner, Entity mesh) where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(owner))
            {
                em.AddComponentData(mesh, em.GetComponentData<T>(owner));
            }
        }
    }
}
