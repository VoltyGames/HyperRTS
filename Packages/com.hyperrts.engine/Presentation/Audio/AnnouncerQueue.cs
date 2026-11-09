using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Announcements;
using HyperRTS.Simulation.Cues.Sounds;
using System.Collections.Generic;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// The local player's waiting announcer lines: each alert respects its line's cooldown, the most important waiting
    /// line (lowest cue priority, then oldest) speaks next, and lines left waiting too long are dropped.
    /// </summary>
    public sealed class AnnouncerQueue
    {
        private readonly Dictionary<AnnouncementKind, Throttle> _throttles = new();
        private readonly List<(SoundCue Cue, double Queued)> _waiting = new();

        public int Count => _waiting.Count;

        /// <summary>Queues the set's line for the alert; false when it is silent or still cooling down.</summary>
        public bool Offer(AnnouncerSet set, AnnouncementKind kind, double now)
        {
            if (!set.TryGetLine(kind, out var line) || line.cue == null)
            {
                return false;
            }

            _throttles.TryGetValue(kind, out var throttle);
            if (!throttle.TryPass(now, line.cooldown))
            {
                return false;
            }

            _throttles[kind] = throttle;
            _waiting.Add((line.cue, now));
            return true;
        }

        /// <summary>The line to speak now, after dropping those waiting past <paramref name="maxWait"/>; null if none.</summary>
        public SoundCue Next(double now, float maxWait)
        {
            for (var i = _waiting.Count - 1; i >= 0; i--)
            {
                if (now - _waiting[i].Queued > maxWait)
                {
                    _waiting.RemoveAt(i);
                }
            }

            if (_waiting.Count == 0)
            {
                return null;
            }

            var best = 0;
            for (var i = 1; i < _waiting.Count; i++)
            {
                if (_waiting[i].Cue.priority < _waiting[best].Cue.priority)
                {
                    best = i;
                }
            }

            var cue = _waiting[best].Cue;
            _waiting.RemoveAt(best);
            return cue;
        }
    }
}
