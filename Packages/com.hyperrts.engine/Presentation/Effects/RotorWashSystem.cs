using HyperRTS.Core;
using HyperRTS.Presentation.Animation;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Streams each drawn rotorcraft's <see cref="RotorWash"/>: the rig for the ground material under it, thicker the
    /// lower it flies and the faster its rotors turn.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RotorWashSystem : SystemBase
    {
        private readonly EffectStreams _streams = new();
        private EffectPool _pool;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            RequireForUpdate<RotorWash>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            var deltaTime = SystemAPI.Time.DeltaTime;
            var hasGround = SystemAPI.TryGetSingleton(out GroundMaterials ground);
            // No terrain is a flat world at height 0 (TerrainHeight's default).
            SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            _streams.BeginFrame();
            foreach (var (wash, spin, place, entity) in SystemAPI
                         .Query<RefRO<RotorWash>, RefRO<RotorSpin>, RefRO<LocalToWorld>>()
                         .WithNone<FogHidden, Inside>().WithEntityAccess())
            {
                var position = place.ValueRO.Position;
                var below = new float3(position.x, terrain.Height(position.xz), position.z);
                var strength = spin.ValueRO.Amount * math.saturate(1f - (position.y - below.y) / wash.ValueRO.Height);
                if (strength <= 0f)
                {
                    continue;
                }

                var material = hasGround ? ground.At(below.xz) : SurfaceMaterial.None;
                var state = _pool.Step(_streams, wash.ValueRO.Effects.Value.RigFor(material), entity, 0, below);
                state?.Emit(Quaternion.identity, deltaTime, strength);
            }

            _streams.EndFrame();
        }
    }
}
