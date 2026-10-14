using HyperRTS.Core;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Streams each drawn vehicle's <see cref="MotionDust"/>: the rig for the ground material under its socket,
    /// stronger with speed up to the full-rate speed.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MotionDustSystem : SystemBase
    {
        private readonly EffectStreams _streams = new();
        private EffectPool _pool;
        private Camera _camera;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            RequireForUpdate<MotionDust>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _camera = EffectRange.Main(_camera);
            var deltaTime = SystemAPI.Time.DeltaTime;
            var hasGround = SystemAPI.TryGetSingleton(out GroundMaterials ground);
            _streams.BeginFrame();
            foreach (var (dust, place, entity) in SystemAPI.Query<RefRO<MotionDust>, RefRO<LocalToWorld>>()
                         .WithNone<DisableRendering>().WithEntityAccess())
            {
                var socket = math.transform(place.ValueRO.Value, dust.ValueRO.Socket);
                var material = hasGround ? ground.At(socket.xz) : SurfaceMaterial.None;
                var rig = _pool.Get(dust.ValueRO.Effects.Value.RigFor(material));
                if (rig == null || !EffectRange.InRange(_camera, rig, socket))
                {
                    continue;
                }

                var state = _streams.Step(entity, 0, socket);
                var speed = deltaTime > 0f ? Vector3.Distance(state.From, state.To) / deltaTime : 0f;
                var strength = Mathf.Clamp01(speed / dust.ValueRO.FullSpeed);
                rig.Stream(state.From, state.To, place.ValueRO.Rotation, deltaTime, strength,
                    EffectDetail.Density, ref state.Carry);
            }

            _streams.EndFrame();
        }
    }
}
