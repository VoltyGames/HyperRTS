using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Gives the extra material entities of a multi-material bone-animated renderer the clips and bounds of the
    /// renderer's own entity, since Entities Graphics bakes each material as a child entity without them.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateBefore(typeof(VATBoundsBakingSystem))]
    public partial struct BoneSubmeshBakingSystem : ISystem
    {
        private EntityQuery _meshes;

        public void OnCreate(ref SystemState state)
        {
            _meshes = new EntityQueryBuilder(Allocator.Temp).WithAll<Parent, MaterialMeshInfo>().WithNone<LowerClip>()
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
                if (!em.HasComponent<LowerClip>(owner))
                {
                    continue;
                }

                em.AddComponentData(mesh, em.GetComponentData<LowerClip>(owner));
                em.AddComponentData(mesh, em.GetComponentData<UpperClip>(owner));
                em.AddComponentData(mesh, em.GetComponentData<LowerFade>(owner));
                em.AddComponentData(mesh, em.GetComponentData<UpperFade>(owner));
                em.AddComponentData(mesh, em.GetComponentData<VATBounds>(owner));
            }
        }
    }
}
