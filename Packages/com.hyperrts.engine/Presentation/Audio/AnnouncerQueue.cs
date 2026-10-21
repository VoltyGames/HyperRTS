using System.Collections.Generic;
using HyperRTS.Simulation.Cues;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// The local player's waiting announcer lines: each alert respects its line's cooldown, the most important waiting
    /// line (lowest cue priority, then oldest) speaks next, and lines left waiting too long are dropped.
    /// </summary>
    public sealed class AnnouncerQueue
    {
        private readonly Dictionary<CueSlot, double> _next = new();
        private readonly List<(SoundCue Cue, double Queued)> _waiting = new();

        public int Count => _waiting.Count;

        /// <summary>Queues the set's line for the alert; false when it is silent or still cooling down.</summary>
        public bool Offer(AnnouncerSet set, CueSlot slot, double now)
        {
            var line = set.LineFor(slot);
            var cooling = _next.TryGetValue(slot, out var next) && now < next;
            if (line.cue == null || cooling)
            {
                return false;
            }

            _next[slot] = now + line.cooldown;
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
