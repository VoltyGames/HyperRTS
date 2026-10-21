using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// Plays the under fire voice of a unit hit this frame after a quiet spell (<see cref="UnderFireVoice"/>), at most
    /// once per <see cref="PlayerInterval"/> for each player.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct UnderFireSystem : ISystem
    {
        /// <summary>Seconds between two under fire voices of one player.</summary>
        public const float PlayerInterval = 5f;

        private CueWriter _cues;
        private ComponentLookup<UnderFireVoice> _voices;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<Health> _health;
        private NativeArray<double> _nextByPlayer;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
            _voices = state.GetComponentLookup<UnderFireVoice>();
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _health = state.GetComponentLookup<Health>(true);
            _nextByPlayer = new NativeArray<double>(Faction.Max + 1, Allocator.Persistent);
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<GameEvents>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => _nextByPlayer.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());
            _voices.Update(ref state);
            _transforms.Update(ref state);
            _health.Update(ref state);
            new CallOutJob
            {
                Now = SystemAPI.Time.ElapsedTime,
                Cues = _cues,
                Voices = _voices,
                Transforms = _transforms,
                Health = _health,
                NextByPlayer = _nextByPlayer,
            }.Schedule();
        }

        // Single-threaded: hits on many units share one cue queue and one throttle per player.
        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct CallOutJob : IJobEntity
        {
            public double Now;
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
                var quiet = voice.Strike(Now);
                Voices[unit] = voice;
                return quiet;
            }

            /// <summary>A unit killed by the hit stays silent; its death cue speaks instead.</summary>
            private void CallOut(in EntityDamaged hit)
            {
                var alive = Health[hit.Entity].Current > 0f;
                if (!alive || Now < NextByPlayer[hit.Faction])
                {
                    return;
                }

                var place = Transforms[hit.Entity];
                if (Cues.Play(hit.Entity, CueSlot.UnderFire, place, place.Forward(), hit.Faction))
                {
                    NextByPlayer[hit.Faction] = Now + PlayerInterval;
                }
            }
        }
    }
}
