using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Cues.Voice
{
    /// <summary>
    /// Plays the under fire voice of a unit hit this frame after a quiet spell (<see cref="UnderFireVoice"/>), at most
    /// once per <see cref="VoiceRules.UnderFireInterval"/> for each player.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct UnderFireSystem : ISystem
    {
        private CueWriter _cues;
        private NativeArray<double> _nextByPlayer;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
            _nextByPlayer = new NativeArray<double>(Owner.Max + 1, Allocator.Persistent);
            state.RequireForUpdate<GameEvents>();
            state.RequireForUpdate<VoiceRules>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => _nextByPlayer.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cues.Update(ref state);
            new CallOutJob
            {
                Now = SystemAPI.Time.ElapsedTime,
                Rules = SystemAPI.GetSingleton<VoiceRules>(),
                Cues = _cues,
                Voices = SystemAPI.GetComponentLookup<UnderFireVoice>(false),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Health = SystemAPI.GetComponentLookup<Health>(true),
                NextByPlayer = _nextByPlayer,
            }.Schedule();
        }

        // Single-threaded: hits on many units share one cue queue and one throttle per player.
        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct CallOutJob : IJobEntity
        {
            public double Now;
            public VoiceRules Rules;
            public CueWriter Cues;
            public ComponentLookup<UnderFireVoice> Voices;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<Health> Health;
            public NativeArray<double> NextByPlayer;

            private void Execute(in DynamicBuffer<EntityDamaged> damaged)
            {
                foreach (var hit in damaged)
                {
                    if (BrokeQuiet(hit.Entity))
                    {
                        CallOut(hit);
                    }
                }
            }

            private bool BrokeQuiet(Entity unit)
            {
                if (!Voices.HasComponent(unit))
                {
                    return false;
                }

                var voice = Voices[unit];
                var quiet = voice.Strike(Now, Rules.UnderFireQuiet);
                Voices[unit] = voice;
                return quiet;
            }

            /// <summary>A unit killed by the hit stays silent; its death cue speaks instead.</summary>
            private void CallOut(in EntityDamaged hit)
            {
                var alive = Health[hit.Entity].Current > 0f;
                if (!alive || Now < NextByPlayer[hit.Owner])
                {
                    return;
                }

                var place = Transforms[hit.Entity];
                if (Cues.Play(hit.Entity, CueSlot.UnderFire, place, place.Forward(), hit.Owner))
                {
                    NextByPlayer[hit.Owner] = Now + Rules.UnderFireInterval;
                }
            }
        }
    }
}
