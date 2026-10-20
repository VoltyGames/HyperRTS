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

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            RequireForUpdate<MotionDust>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            var deltaTime = SystemAPI.Time.DeltaTime;
            var hasGround = SystemAPI.TryGetSingleton(out GroundMaterials ground);
            _streams.BeginFrame();
            foreach (var (dust, place, entity) in SystemAPI.Query<RefRO<MotionDust>, RefRO<LocalToWorld>>()
                         .WithNone<DisableRendering>().WithEntityAccess())
            {
                var socket = math.transform(place.ValueRO.Value, dust.ValueRO.Socket);
                var material = hasGround ? ground.At(socket.xz) : SurfaceMaterial.None;
                var state = _pool.Step(_streams, dust.ValueRO.Effects.Value.RigFor(material), entity, 0, socket);
                if (state == null)
                {
                    continue;
                }

                var speed = deltaTime > 0f ? Vector3.Distance(state.From, state.To) / deltaTime : 0f;
                var strength = Mathf.Clamp01(speed / dust.ValueRO.FullSpeed);
                state.Emit(place.ValueRO.Rotation, deltaTime, strength);
            }

            _streams.EndFrame();
        }
    }
}
