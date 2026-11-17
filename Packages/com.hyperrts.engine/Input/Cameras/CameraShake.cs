using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Input.Cameras
{
    /// <summary>
    /// The camera's shake from effect bursts near its focus: summed sines under a fading envelope, capped however many
    /// bursts stack.
    /// </summary>
    [Serializable]
    public class CameraShake
    {
        // Two unrelated frequencies per axis, so the wobble never settles into a visible rhythm.
        private const float SlowWave = 2f * Mathf.PI * 11f;
        private const float FastWave = 2f * Mathf.PI * 17f;

        [Tooltip("Effect bursts near the focus shake the camera; a player setting.")]
        public bool enabled = true;

        [Tooltip("Most the shake moves the camera at its lowest zoom, however many bursts stack; it moves further in step with height.")]
        [Unit("m")]
        [Min(0.01f)]
        public float maxOffset = 0.15f;

        [Tooltip("Most degrees the shake turns the camera, reached at the full offset.")]
        [Min(0f)]
        public float maxAngle = 0.5f;

        [Tooltip("Seconds a burst's shake takes to die out.")]
        [Min(0.01f)]
        public float duration = 0.3f;

        [Tooltip("Distance from the focus at which a burst no longer shakes the camera.")]
        [Unit("m")]
        [Min(0.01f)]
        public float reach = 30f;

        // x is a jolt's amplitude in metres as it was taken, y its age in seconds.
        private readonly List<Vector2> _jolts = new();
        private float _time;
        private float _zoomOut = 1f;

        /// <summary>This frame's offset, applied on top of the camera's pose.</summary>
        public Pose Offset { get; private set; } = Pose.identity;

        /// <summary>
        /// Takes the presented world's new jolts, then advances; while not <see cref="enabled"/> it drops them all.
        /// <paramref name="zoomOut"/> is the camera's height over its lowest.
        /// </summary>
        public void Step(Vector3 focus, float zoomOut, float deltaTime)
        {
            if (!enabled)
            {
                _jolts.Clear();
                Offset = Pose.identity;
                return;
            }

            // A jolt weakens with the square root of the height, so a blast is still felt at working zoom, while its
            // offset grows with the height, so it moves the picture as far at any zoom.
            _zoomOut = zoomOut;
            if (TryGetWorldJolts(out var jolts))
            {
                foreach (var jolt in jolts)
                {
                    Add(jolt, focus, 1f / Mathf.Sqrt(zoomOut));
                }
            }

            Advance(deltaTime);
        }

        /// <summary>
        /// Starts a jolt, weakened by its distance from <paramref name="focus"/> and by <paramref name="closeness"/>
        /// (1 at the closest zoom).
        /// </summary>
        public void Add(in CameraJolt jolt, Vector3 focus, float closeness)
        {
            var falloff = 1f - Vector3.Distance(jolt.Position, focus) / reach;
            if (falloff > 0f)
            {
                _jolts.Add(new Vector2(jolt.Strength * falloff * closeness, 0f));
            }
        }

        /// <summary>Ages the jolts, drops those past <see cref="duration"/> and sets <see cref="Offset"/>.</summary>
        public void Advance(float deltaTime)
        {
            _time += deltaTime;
            var amplitude = 0f;
            for (var i = _jolts.Count - 1; i >= 0; i--)
            {
                var jolt = _jolts[i];
                jolt.y += deltaTime;
                if (jolt.y >= duration)
                {
                    _jolts.RemoveAt(i);
                    continue;
                }

                _jolts[i] = jolt;
                var left = 1f - jolt.y / duration;
                amplitude += jolt.x * left * left;
            }

            var share = Mathf.Min(amplitude, maxOffset) / maxOffset;
            Offset = new Pose(Wobble(0f) * (share * maxOffset * _zoomOut), Quaternion.Euler(Wobble(10f) * (share * maxAngle)));
        }

        /// <summary>A smooth direction no longer than 1, from three phase-shifted waves.</summary>
        private Vector3 Wobble(float phase) =>
            Vector3.ClampMagnitude(new Vector3(Wave(phase), Wave(phase + 2f), Wave(phase + 4f)), 1f);

        private float Wave(float phase) =>
            0.5f * (Mathf.Sin(_time * SlowWave + phase) + Mathf.Sin(_time * FastWave + phase * 1.7f));

        private static bool TryGetWorldJolts(out List<CameraJolt> jolts)
        {
            jolts = null;
            if (!DefaultWorld.TryGet(out var world))
            {
                return false;
            }

            jolts = world.GetExistingSystemManaged<EffectPoolSystem>()?.Pool.Jolts;
            return jolts != null;
        }
    }
}
