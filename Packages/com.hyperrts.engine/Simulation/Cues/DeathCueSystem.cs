using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Cues.Sounds;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Plays the death cue of everything <c>DeathSystem</c> marked <see cref="Dead"/> this frame.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct DeathCueSystem : ISystem
    {
        private CueWriter _cues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cues.Update(ref state);
            new DeathJob { Cues = _cues }.Schedule();
        }

        // Single-threaded: every death appends to the one cue queue.
        [BurstCompile]
        [WithAll(typeof(Dead))]
        [WithAny(typeof(EntitySound), typeof(EntityEffect), typeof(DeathRemains))]
        private partial struct DeathJob : IJobEntity
        {
            public CueWriter Cues;

            private void Execute(Entity entity, in LocalTransform transform, in Faction faction) =>
                Cues.Play(entity, CueSlot.Death, transform, transform.Forward(), faction.Value);
        }
    }
}
