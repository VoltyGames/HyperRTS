using System;
using Unity.Collections;

namespace HyperRTS.Simulation.Cues.Announcements
{
    /// <summary>
    /// Sends each player at most one announcement of a kind per <see cref="Interval"/>, so a siege or a battle sends a
    /// trickle of alerts rather than one per hit. The listener's announcer applies the longer per-line cooldowns.
    /// </summary>
    public struct AnnouncementGate : IDisposable
    {
        public const float Interval = 1f;

        private NativeHashMap<ushort, Throttle> _next;

        public AnnouncementGate(Allocator allocator) => _next = new NativeHashMap<ushort, Throttle>(16, allocator);

        /// <summary>True, closing the gate for <see cref="Interval"/>, when the owner may hear this kind now.</summary>
        public bool TryPass(AnnouncementKind kind, byte owner, double now)
        {
            var key = (ushort)(owner << 8 | kind.Value);
            _next.TryGetValue(key, out var throttle);
            var passed = throttle.TryPass(now, Interval);
            _next[key] = throttle;
            return passed;
        }

        public void Dispose() => _next.Dispose();
    }
}
