using System;
using HyperRTS.Simulation.Common;
using Unity.Collections;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// Sends each player at most one announcement of a kind per <see cref="Interval"/>, so a siege or a battle sends a
    /// trickle of alerts rather than one per hit. The listener's announcer applies the longer per-line cooldowns.
    /// </summary>
    public struct AnnouncementGate : IDisposable
    {
        public const float Interval = 1f;

        private const int Kinds = CueSlot.AnnounceUpgradeComplete - CueSlot.AnnounceUnitLost + 1;

        private NativeArray<double> _next;

        public AnnouncementGate(Allocator allocator) =>
            _next = new NativeArray<double>((Faction.Max + 1) * Kinds, allocator);

        /// <summary>True, closing the gate for <see cref="Interval"/>, when the faction may hear this kind now.</summary>
        public bool TryPass(CueSlot slot, byte faction, double now)
        {
            var index = faction * Kinds + (slot - CueSlot.AnnounceUnitLost);
            if (now < _next[index])
            {
                return false;
            }

            _next[index] = now + Interval;
            return true;
        }

        public void Dispose() => _next.Dispose();
    }
}
