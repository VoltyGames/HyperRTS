using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Audio
{
    /// <summary>Plays the death cue of everything <c>DeathSystem</c> marked <see cref="Dead"/> this frame.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct DeathSoundSystem : ISystem
    {
        private SoundWriter _sounds;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sounds = new SoundWriter(ref state);
            state.RequireForUpdate<SoundQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _sounds.Update(ref state, SystemAPI.GetSingletonEntity<SoundQueue>());
            new DeathJob { Sounds = _sounds }.Schedule();
        }

        // Single-threaded: every death appends to the one sound queue.
        [BurstCompile]
        [WithAll(typeof(Dead), typeof(EntitySound))]
        private partial struct DeathJob : IJobEntity
        {
            public SoundWriter Sounds;

            private void Execute(Entity entity, in LocalTransform transform, in Faction faction) =>
                Sounds.Play(entity, SoundSlot.Death, transform.Position, faction.Value);
        }
    }
}
