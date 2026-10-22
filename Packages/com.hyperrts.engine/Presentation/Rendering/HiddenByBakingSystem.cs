using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Gives every mesh and gameplay root a <see cref="HiddenBy"/> at bake time, so hiding never adds components.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct HiddenByBakingSystem : ISystem
    {
        private EntityQuery _unmarked;

        public void OnCreate(ref SystemState state)
        {
            _unmarked = new EntityQueryBuilder(Allocator.Temp).WithAny<MaterialMeshInfo, EntityInfo>().WithNone<HiddenBy>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities |
                             EntityQueryOptions.IgnoreComponentEnabledState).Build(ref state);
        }

        public void OnUpdate(ref SystemState state) => state.EntityManager.AddComponent<HiddenBy>(_unmarked);
    }
}
