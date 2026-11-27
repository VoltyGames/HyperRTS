using System;
using HyperRTS.Simulation.Cues.Sounds;
using UnityEngine;

namespace HyperRTS.Presentation.Audio;

/// <summary>
/// How a 3D cue fades and dulls with distance, as the rolloff and low-pass curves Unity reads, sampled from the
/// cue's minimum and maximum distance. Both curves run over distance divided by the maximum, as Unity's do.
/// </summary>
public sealed class DistanceCurves
{
    /// <summary>Low-pass cutoff in Hz at a cue's maximum distance: far sounds keep their body, lose the crack.</summary>
    public const float FarCutoff = 2500f;

    // Unity reads a low-pass curve's 0 to 1 as 0 to 22 kHz.
    private const float FullCutoff = 22000f;
    private const int Samples = 16;

    private readonly float _min;
    private readonly float _max;

    public DistanceCurves(SoundCue cue)
    {
        _min = cue.minDistance;
        _max = cue.maxDistance;
        Rolloff = Sample(Volume);
        Cutoff = Sample(CutoffShare);
    }

    public AnimationCurve Rolloff { get; }

    public AnimationCurve Cutoff { get; }

    /// <summary>False once the cue's distances were edited, so the curves are sampled again.</summary>
    public bool Fits(SoundCue cue) => cue.minDistance == _min && cue.maxDistance == _max;

    /// <summary>
    /// Full volume within <paramref name="min"/>, then the inverse-distance fade of open air, tapered so it reaches
    /// silence at <paramref name="max"/> instead of cutting off.
    /// </summary>
    public static float Volume(float distance, float min, float max)
    {
        if (distance >= max)
        {
            return 0f;
        }

        if (distance <= min)
        {
            return 1f;
        }

        var share = (distance - min) / (max - min);
        return min / distance * (1f - share * share);
    }

    /// <summary>
    /// The low-pass cutoff as a share of 22 kHz: open within the minimum, then closing by even octaves out to
    /// <see cref="FarCutoff"/>.
    /// </summary>
    public static float CutoffShare(float distance, float min, float max)
    {
        var share = Mathf.Clamp01((distance - min) / (max - min));
        return Mathf.Pow(FarCutoff / FullCutoff, share);
    }

    // Keys crowd near the minimum, where the fade bends most, and join in straight segments so nothing overshoots.
    private AnimationCurve Sample(Func<float, float, float, float> shape)
    {
        var keys = new Keyframe[Samples + 2];
        keys[0] = new Keyframe(0f, shape(0f, _min, _max));
        for (var i = 0; i <= Samples; i++)
        {
            var t = (float)i / Samples;
            var distance = _min + (_max - _min) * t * t;
            keys[i + 1] = new Keyframe(distance / _max, shape(distance, _min, _max));
        }

        for (var i = 0; i < keys.Length; i++)
        {
            keys[i].inTangent = i > 0 ? Slope(keys[i - 1], keys[i]) : 0f;
            keys[i].outTangent = i < keys.Length - 1 ? Slope(keys[i], keys[i + 1]) : 0f;
        }

        return new AnimationCurve(keys);
    }

    private static float Slope(Keyframe a, Keyframe b) => (b.value - a.value) / Mathf.Max(b.time - a.time, 1e-6f);
}
