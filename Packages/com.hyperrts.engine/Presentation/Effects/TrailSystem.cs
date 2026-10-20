using HyperRTS.Core;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>Streams each drawn projectile's <see cref="EffectTrail"/> along the path it moved this frame.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrailSystem : SystemBase
    {
        private readonly EffectStreams _streams = new();
        private EffectPool _pool;
        private Camera _camera;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            RequireForUpdate<EffectTrail>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _camera = EffectRange.Main(_camera);
            var deltaTime = SystemAPI.Time.DeltaTime;
            _streams.BeginFrame();
            foreach (var (trail, place, entity) in SystemAPI.Query<RefRO<EffectTrail>, RefRO<LocalToWorld>>()
                         .WithNone<DisableRendering>().WithEntityAccess())
            {
                var rig = trail.ValueRO.Rig.IsValid() ? _pool.Get(trail.ValueRO.Rig.Value) : null;
                Vector3 position = place.ValueRO.Position;
                if (rig == null || !EffectRange.InRange(_camera, rig, position))
                {
                    continue;
                }

                var state = _streams.Step(entity, 0, position);
                if (!state.Ready)
                {
                    continue;
                }

                rig.Pick(EffectStreams.Key(entity, 0)).Stream(state.From, state.To, EffectPose.Facing(state.To - state.From), deltaTime, 1f,
                    EffectDetail.Density, ref state.Carry);
            }

            _streams.EndFrame();
        }
    }
}
