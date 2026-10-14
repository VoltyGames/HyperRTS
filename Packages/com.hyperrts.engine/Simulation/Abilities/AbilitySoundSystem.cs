using HyperRTS.Core;
using HyperRTS.Simulation.Audio;
using HyperRTS.Simulation.Effects;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>Plays each <see cref="AbilityActivation"/>'s ability cue and effect at the aim point.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AbilitySystem))]
    public partial struct AbilitySoundSystem : ISystem
    {
        private SoundWriter _sounds;
        private EffectWriter _effects;
        private ComponentLookup<LocalTransform> _transforms;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sounds = new SoundWriter(ref state);
            _effects = new EffectWriter(ref state);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            state.RequireForUpdate<SoundQueue>();
            state.RequireForUpdate<EffectQueue>();
            state.RequireForUpdate<AbilityEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _sounds.Update(ref state, SystemAPI.GetSingletonEntity<SoundQueue>());
            _effects.Update(ref state, SystemAPI.GetSingletonEntity<EffectQueue>());
            _transforms.Update(ref state);
            new CastJob { Sounds = _sounds, Effects = _effects, Transforms = _transforms }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(AbilityEvents))]
        private partial struct CastJob : IJobEntity
        {
            public SoundWriter Sounds;
            public EffectWriter Effects;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

            private void Execute(in DynamicBuffer<AbilityActivation> activations)
            {
                foreach (var cast in activations)
                {
                    Sounds.Play(cast.Caster, SoundSlot.Ability, cast.Position, cast.Faction);
                    var direction = Direction(cast);
                    var aim = LocalTransform.FromPositionRotation(cast.Position,
                        quaternion.LookRotationSafe(direction, math.up()));
                    Effects.Play(cast.Caster, EffectSlot.Ability, aim, direction, cast.Faction);
                }
            }

            /// <summary>
            /// Level, from the caster toward the aim point. A caster gone this frame has no rig to play.
            /// </summary>
            private float3 Direction(in AbilityActivation cast)
            {
                var forward = new float3(0f, 0f, 1f);
                if (!Transforms.TryGetComponent(cast.Caster, out var caster))
                {
                    return forward;
                }

                var toward = cast.Position - caster.Position;
                toward.y = 0f;
                return math.normalizesafe(toward, caster.Forward());
            }
        }
    }
}
