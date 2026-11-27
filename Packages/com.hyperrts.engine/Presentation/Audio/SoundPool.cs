using System.Collections.Generic;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Sounds;

namespace HyperRTS.Presentation.Audio;

/// <summary>
/// Bookkeeping for a fixed set of voices: which cue each plays, how loud it is heard and until when. Enforces each
/// cue's instance limit and cooldown and, when all are busy, lets a sound take the voice of a less important or
/// quieter one.
/// </summary>
public sealed class SoundPool(int capacity)
{
    /// <summary>Sounds heard quieter than this (about -40 dB) are not worth a voice.</summary>
    public const float Inaudible = 0.01f;

    private readonly SoundCue[] _cues = new SoundCue[capacity];
    private readonly int[] _priorities = new int[capacity];
    private readonly float[] _loudness = new float[capacity];
    private readonly double[] _ends = new double[capacity];
    private readonly Dictionary<SoundCue, Throttle> _rested = [];

    public int Capacity => _cues.Length;

    /// <summary>
    /// Claims a voice for <paramref name="duration"/> seconds for a sound heard at <paramref name="loudness"/> (its
    /// volume after distance, 0 to 1); -1 when the cue may not play now.
    /// </summary>
    public int Acquire(SoundCue cue, float loudness, double now, double duration)
    {
        if (loudness < Inaudible || Playing(cue, now) >= cue.maxInstances)
        {
            return -1;
        }

        if (cue.cooldown > 0f && _rested.TryGetValue(cue, out var rested) && !rested.IsOpen(now))
        {
            return -1;
        }

        var voice = FreeOrWeakest(now);
        if (IsBusy(voice, now) && !Outranks(cue.priority, loudness, voice))
        {
            return -1;
        }

        _cues[voice] = cue;
        _priorities[voice] = cue.priority;
        _loudness[voice] = loudness;
        _ends[voice] = now + duration;
        if (cue.cooldown > 0f)
        {
            _rested[cue] = new Throttle { Next = now + cue.cooldown };
        }

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

    /// <summary>A free voice, else the busy one least important (highest priority number), then quietest.</summary>
    private int FreeOrWeakest(double now)
    {
        var weakest = 0;
        for (var i = 0; i < Capacity; i++)
        {
            if (!IsBusy(i, now))
            {
                return i;
            }

            if (Outranks(_priorities[weakest], _loudness[weakest], i))
            {
                weakest = i;
            }
        }

        return weakest;
    }

    private bool Outranks(int priority, float loudness, int voice)
    {
        if (priority != _priorities[voice])
        {
            return priority < _priorities[voice];
        }

        return loudness > _loudness[voice];
    }
}
