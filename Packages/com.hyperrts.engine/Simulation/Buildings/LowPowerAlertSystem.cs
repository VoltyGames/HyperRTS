using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Announces low power when a player's <see cref="PowerGrid"/> starts drawing more than it makes, at most once per
    /// <see cref="AnnouncementGate.Interval"/> so a flickering grid doesn't repeat it.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct LowPowerAlertSystem : ISystem
    {
        private CueWriter _cues;
        private NativeArray<bool> _wasLow;
        private AnnouncementGate _gate;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
            _wasLow = new NativeArray<bool>(PlayerRegistry.Capacity, Allocator.Persistent);
            _gate = new AnnouncementGate(Allocator.Persistent);
            state.RequireForUpdate<CueQueue>();
            // Without an announcer nobody hears alerts, so none are sent.
            state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<AnnouncerVoice>()));
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _wasLow.Dispose();
            _gate.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());
            new AlertJob { Now = SystemAPI.Time.ElapsedTime, Cues = _cues, WasLow = _wasLow, Gate = _gate }.Schedule();
        }

        // Single-threaded: every player's alert appends to the one cue queue.
        [BurstCompile]
        private partial struct AlertJob : IJobEntity
        {
            public double Now;
            public CueWriter Cues;
            public NativeArray<bool> WasLow;
            public AnnouncementGate Gate;

            private void Execute(in PowerGrid grid, in Player player)
            {
                var faction = player.Faction;
                var fell = grid.IsLow && !WasLow[faction];
                WasLow[faction] = grid.IsLow;
                if (fell && Gate.TryPass(AnnouncementKind.LowPower, faction, Now))
                {
                    Cues.Announce(AnnouncementKind.LowPower, faction, float3.zero, 0);
                }
            }
        }
    }
}
