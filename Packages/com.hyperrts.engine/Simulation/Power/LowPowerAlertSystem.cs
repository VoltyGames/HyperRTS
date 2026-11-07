using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Announcements;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Power
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
            _cues.Update(ref state);
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
                var owner = player.Owner;
                var fell = grid.IsLow && !WasLow[owner];
                WasLow[owner] = grid.IsLow;
                if (fell && Gate.TryPass(AnnouncementKind.LowPower, owner, Now))
                {
                    Cues.Announce(AnnouncementKind.LowPower, owner, float3.zero, 0);
                }
            }
        }
    }
}
