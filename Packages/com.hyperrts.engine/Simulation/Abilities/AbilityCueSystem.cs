using HyperRTS.Core;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// Plays each <see cref="AbilityActivation"/>'s ability cue at the aim point, and the caster's owner-only ability
    /// voice at the caster.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AbilitySystem))]
    public partial struct AbilityCueSystem : ISystem
    {
        private CueWriter _cues;
        private ComponentLookup<LocalTransform> _transforms;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<AbilityEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());
            _transforms.Update(ref state);
            new CastJob { Cues = _cues, Transforms = _transforms }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(AbilityEvents))]
        private partial struct CastJob : IJobEntity
        {
            public CueWriter Cues;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

            private void Execute(in DynamicBuffer<AbilityActivation> activations)
            {
                foreach (var cast in activations)
                {
                    var direction = Direction(cast);
                    var aim = LocalTransform.FromPositionRotation(cast.Position,
                        quaternion.LookRotationSafe(direction, math.up()));
                    Cues.Play(cast.Caster, CueSlot.Ability, aim, direction, cast.Faction);
                    Speak(cast);
                }
            }

            /// <summary>Player support powers have no body to speak from.</summary>
            private void Speak(in AbilityActivation cast)
            {
                if (Transforms.TryGetComponent(cast.Caster, out var caster))
                {
                    Cues.Play(cast.Caster, CueSlot.AbilityVoice, caster, caster.Forward(), cast.Faction);
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
