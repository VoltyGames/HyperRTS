using HyperRTS.Core;
using HyperRTS.Simulation.Audio;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>Plays the caster's ability cue, at the aim point, for every <see cref="AbilityActivation"/> this frame.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AbilitySystem))]
    public partial struct AbilitySoundSystem : ISystem
    {
        private SoundWriter _sounds;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sounds = new SoundWriter(ref state);
            state.RequireForUpdate<SoundQueue>();
            state.RequireForUpdate<AbilityEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _sounds.Update(ref state, SystemAPI.GetSingletonEntity<SoundQueue>());
            new CastJob { Sounds = _sounds }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(AbilityEvents))]
        private partial struct CastJob : IJobEntity
        {
            public SoundWriter Sounds;

            private void Execute(in DynamicBuffer<AbilityActivation> activations)
            {
                foreach (var cast in activations)
                {
                    Sounds.Play(cast.Caster, SoundSlot.Ability, cast.Position, cast.Faction);
                }
            }
        }
    }
}
