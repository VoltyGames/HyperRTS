using HyperRTS.Core;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>Streams each drawn projectile's <see cref="EffectTrail"/> along the path it moved this frame.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrailSystem : SystemBase
    {
        private readonly EffectStreams _streams = new();
        private EffectPool _pool;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            RequireForUpdate<EffectTrail>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            var deltaTime = SystemAPI.Time.DeltaTime;
            _streams.BeginFrame();
            foreach (var (trail, place, entity) in SystemAPI.Query<RefRO<EffectTrail>, RefRO<LocalToWorld>>()
                         .WithNone<DisableRendering>().WithEntityAccess())
            {
                var prefab = trail.ValueRO.Rig.IsValid() ? trail.ValueRO.Rig.Value : null;
                var state = _pool.Step(_streams, prefab, entity, 0, place.ValueRO.Position);
                state?.Emit(EffectPose.Facing(state.To - state.From), deltaTime, 1f);
            }

            _streams.EndFrame();
        }
    }
}
