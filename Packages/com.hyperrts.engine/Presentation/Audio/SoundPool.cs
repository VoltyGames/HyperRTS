using HyperRTS.Simulation.Audio;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// Bookkeeping for a fixed set of voices: which cue each plays and until when. Enforces each cue's instance limit
    /// and lets an important cue take the voice of the least important one when all are busy.
    /// </summary>
    public sealed class SoundPool
    {
        private readonly SoundCue[] _cues;
        private readonly int[] _priorities;
        private readonly double[] _ends;

        public SoundPool(int capacity)
        {
            _cues = new SoundCue[capacity];
            _priorities = new int[capacity];
            _ends = new double[capacity];
        }

        public int Capacity => _cues.Length;

        /// <summary>Claims a voice for <paramref name="duration"/> seconds; -1 when the cue may not play now.</summary>
        public int Acquire(SoundCue cue, double now, double duration)
        {
            if (Playing(cue, now) >= cue.maxInstances)
            {
                return -1;
            }

            var voice = FreeOrWeakest(now);
            if (IsBusy(voice, now) && _priorities[voice] <= cue.priority)
            {
                return -1;
            }

            _cues[voice] = cue;
            _priorities[voice] = cue.priority;
            _ends[voice] = now + duration;
            return voice;
        }

        public int Playing(SoundCue cue, double now)
        {
            var count = 0;
            for (var i = 0; i < Capacity; i++)
            {
                if (_cues[i] == cue && IsBusy(i, now))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsBusy(int voice, double now) => _ends[voice] > now;

        /// <summary>A free voice, else the busy one with the least important cue (highest priority number).</summary>
        private int FreeOrWeakest(double now)
        {
            var weakest = 0;
            for (var i = 0; i < Capacity; i++)
            {
                if (!IsBusy(i, now))
                {
                    return i;
                }

                if (_priorities[i] > _priorities[weakest])
                {
                    weakest = i;
                }
            }

            return weakest;
        }
    }
}
