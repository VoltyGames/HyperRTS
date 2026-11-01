using HyperRTS.Core;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Effects.Streams
{
    /// <summary>Streams each drawn projectile's <see cref="EffectTrail"/> along the path it moved this frame.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrailSystem : SystemBase
    {
        private EffectStreams _streams;
        private EffectPool _pool;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _streams = new EffectStreams(_pool);
            RequireForUpdate<EffectTrail>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            var deltaTime = SystemAPI.Time.DeltaTime;
            _streams.BeginFrame();
            foreach (var (trail, place, entity) in SystemAPI.Query<RefRO<EffectTrail>, RefRO<LocalToWorld>>()
                         .WithNone<FogHidden, Inside>().WithEntityAccess())
            {
                var prefab = trail.ValueRO.Rig.IsValid() ? trail.ValueRO.Rig.Value : null;
                var state = _streams.Step(prefab, entity, 0, place.ValueRO.Position);
                state?.Emit(EffectPose.Facing(state.To - state.From), deltaTime, 1f);
            }

            _streams.EndFrame();
        }
    }
}
