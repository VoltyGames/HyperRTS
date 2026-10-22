using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Streams each drawn vehicle's <see cref="MotionDust"/>: the rig for the ground material under its socket,
    /// stronger with its drawn speed along its facing up to the full-rate speed. Measured from the drawn motion so it
    /// rises on network clients too; sideways crowd pushes raise little.
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
            foreach (var (dust, place, entity) in SystemAPI
                         .Query<RefRW<MotionDust>, RefRO<LocalToWorld>>()
                         .WithNone<FogHidden, Inside>().WithEntityAccess())
            {
                var speed = dust.ValueRW.DrawnSpeed(place.ValueRO.Position, place.ValueRO.Forward, deltaTime);
                var socket = math.transform(place.ValueRO.Value, dust.ValueRO.Socket);
                var material = hasGround ? ground.At(socket.xz) : SurfaceMaterial.None;
                var state = _pool.Step(_streams, dust.ValueRO.Effects.Value.RigFor(material), entity, 0, socket);
                if (state == null)
                {
                    continue;
                }

                state.Emit(place.ValueRO.Rotation, deltaTime, dust.ValueRO.Strength(speed));
            }

            _streams.EndFrame();
        }
    }
}
