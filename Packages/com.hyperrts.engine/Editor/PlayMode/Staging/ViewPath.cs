using System;
using HyperRTS.Input.Cameras;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>A cinematic move from one aim to another, eased in and out, that may keep a moving group in view.</summary>
    internal sealed class ViewPath : ICameraPath
    {
        private readonly CameraAim _from;
        private readonly CameraAim _to;
        private readonly float _start;
        private readonly float _seconds;
        private readonly Func<float2, float> _groundAt;
        // The point to keep aimed at each frame, or null to move between the aims' points; null once its group is gone.
        private readonly Func<float2?> _track;
        private float2? _tracked;

        public ViewPath(CameraAim from, CameraAim to, float start, float seconds, Func<float2, float> groundAt, Func<float2?> track)
        {
            _from = from;
            _to = to;
            _start = start;
            _seconds = seconds;
            _groundAt = groundAt;
            _track = track;
        }

        public Pose ViewAt(float time)
        {
            var share = _seconds > 0f ? Mathf.SmoothStep(0f, 1f, (time - _start) / _seconds) : 1f;
            var aim = _from.Toward(_to, share);
            if (_track != null)
            {
                _tracked = _track() ?? _tracked;
                aim = aim.At(_tracked ?? aim.Look);
            }

            return aim.ViewOver(_groundAt(aim.Look));
        }
    }
}
