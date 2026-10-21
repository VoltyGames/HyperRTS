using System;
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

        private NativeHashMap<ushort, double> _next;

        public AnnouncementGate(Allocator allocator) => _next = new NativeHashMap<ushort, double>(16, allocator);

        /// <summary>True, closing the gate for <see cref="Interval"/>, when the faction may hear this kind now.</summary>
        public bool TryPass(AnnouncementKind kind, byte faction, double now)
        {
            var key = (ushort)(faction << 8 | kind.Value);
            if (_next.TryGetValue(key, out var next) && now < next)
            {
                return false;
            }

            _next[key] = now + Interval;
            return true;
        }

        public void Dispose() => _next.Dispose();
    }
}
