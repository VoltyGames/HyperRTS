using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>Links the parts of each baked <see cref="MotionRig"/> back to it.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct MotionRigLinkBakingSystem : ISystem
    {
        private EntityQuery _rigs;

        public void OnCreate(ref SystemState state) =>
            _rigs = new EntityQueryBuilder(Allocator.Temp).WithAll<MotionRig, MotionRigPart>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).Build(ref state);

        public void OnUpdate(ref SystemState state)
        {
            using var rigs = _rigs.ToEntityArray(Allocator.Temp);
            foreach (var rig in rigs)
            {
                MotionRigLink.AddAll(state.EntityManager, rig);
            }
        }
    }
}
