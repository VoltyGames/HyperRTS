using System;
using HyperRTS.Input.Cameras;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>A cinematic move from one aim to another, eased in and out, that may keep a moving group in view.</summary>
internal sealed class ViewPath(CameraAim from, CameraAim to, float start, float seconds, Func<float2, float> groundAt, Func<float2?> track) : ICameraPath
{
    private readonly CameraAim _from = from;
    private readonly CameraAim _to = to;
    // The point to keep aimed at each frame, or null to move between the aims' points; null once its group is gone.
    private float2? _tracked;

    public Pose ViewAt(float time)
    {
        var share = seconds > 0f ? Mathf.SmoothStep(0f, 1f, (time - start) / seconds) : 1f;
        var aim = _from.Toward(_to, share);
        if (track != null)
        {
            _tracked = track() ?? _tracked;
            aim = aim.At(_tracked ?? aim.Look);
        }

        return aim.ViewOver(groundAt(aim.Look));
    }
}
