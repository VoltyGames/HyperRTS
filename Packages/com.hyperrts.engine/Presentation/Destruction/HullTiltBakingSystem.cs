using HyperRTS.Simulation.Navigation.Areas;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Gives each <see cref="Blocker"/> the <see cref="HullTilt"/> and matrix <c>HullTiltSystem</c> lays it with; one
    /// whose root is stretched keeps its matrix and lies level. A baking system, not a baker: Simulation cannot name
    /// the tilt.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct HullTiltBakingSystem : ISystem
    {
        private EntityQuery _wrecks;

        public void OnCreate(ref SystemState state)
        {
            _wrecks = new EntityQueryBuilder(Allocator.Temp).WithPresent<Blocker>().WithNone<HullTilt, PostTransformMatrix>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            using var wrecks = _wrecks.ToEntityArray(Allocator.Temp);
            foreach (var wreck in wrecks)
            {
                state.EntityManager.AddComponent<HullTilt>(wreck);
                state.EntityManager.AddComponentData(wreck, new PostTransformMatrix { Value = float4x4.identity });
            }
        }
    }
}
