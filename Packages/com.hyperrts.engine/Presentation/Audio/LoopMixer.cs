using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Cues;
using Unity.Entities;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// Picks which loops play from a fixed set of sources: the nearest within each cue's range and instance limit,
    /// then the nearest overall. A loop that keeps playing keeps its source, so it never restarts.
    /// </summary>
    public sealed class LoopMixer
    {
        /// <summary>Quieter than this is silent, so the loop gives its source back.</summary>
        public const float MinLevel = 0.001f;

        private static readonly Comparison<LoopCandidate> Nearest = (a, b) => a.DistanceSq.CompareTo(b.DistanceSq);

        private readonly List<LoopCandidate> _candidates = new();
        private readonly List<(LoopCandidate Loop, int Source, bool Claimed)> _winners = new();
        private readonly List<int> _released = new();
        private readonly Dictionary<(Entity, LoopSlot), int> _playing = new();
        private readonly Dictionary<(Entity, LoopSlot), int> _kept = new();
        private readonly Dictionary<SoundCue, int> _perCue = new();
        private readonly Stack<int> _free = new();

        public LoopMixer(int capacity)
        {
            Capacity = capacity;
            for (var i = capacity - 1; i >= 0; i--)
            {
                _free.Push(i);
            }
        }

        public int Capacity { get; }

        /// <summary>This frame's playing loops; <c>Claimed</c> marks those that just took their source.</summary>
        public IReadOnlyList<(LoopCandidate Loop, int Source, bool Claimed)> Winners => _winners;

        /// <summary>Sources whose loop stopped this frame.</summary>
        public IReadOnlyList<int> Released => _released;

        public void Begin() => _candidates.Clear();

        /// <summary>Takes a loop unless it is silent, has no clips or is out of its cue's range.</summary>
        public void Offer(in LoopCandidate loop)
        {
            if (loop.Level < MinLevel || loop.Cue == null || loop.Cue.clips.Length == 0)
            {
                return;
            }

            var range = loop.Cue.maxDistance;
            if (loop.DistanceSq > range * range)
            {
                return;
            }

            _candidates.Add(loop);
        }

        public void Resolve()
        {
            _candidates.Sort(Nearest);
            Pick();
            _released.Clear();
            foreach (var (key, source) in _playing)
            {
                if (!_kept.ContainsKey(key))
                {
                    _released.Add(source);
                    _free.Push(source);
                }
            }

            _playing.Clear();
            for (var i = 0; i < _winners.Count; i++)
            {
                var winner = _winners[i];
                if (!_kept.TryGetValue(winner.Loop.Key, out var source))
                {
                    winner = (winner.Loop, _free.Pop(), true);
                    _winners[i] = winner;
                }

                _playing[winner.Loop.Key] = winner.Source;
            }
        }

        /// <summary>Fills the winners nearest first, noting which already hold a source.</summary>
        private void Pick()
        {
            _winners.Clear();
            _kept.Clear();
            _perCue.Clear();
            foreach (var loop in _candidates)
            {
                if (_winners.Count == Capacity)
                {
                    return;
                }

                _perCue.TryGetValue(loop.Cue, out var count);
                if (count >= loop.Cue.maxInstances)
                {
                    continue;
                }

                _perCue[loop.Cue] = count + 1;
                var playing = _playing.TryGetValue(loop.Key, out var source);
                if (playing)
                {
                    _kept[loop.Key] = source;
                }

                _winners.Add((loop, playing ? source : -1, false));
            }
        }
    }
}
