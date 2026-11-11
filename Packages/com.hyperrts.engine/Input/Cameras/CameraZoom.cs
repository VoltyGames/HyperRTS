using System;
using UnityEngine;

namespace HyperRTS.Input.Cameras
{
    /// <summary>The camera's zoom limits, how fast it eases between them and the pitch that goes with each height.</summary>
    [Serializable]
    public class CameraZoom
    {
        [Tooltip("Share of the current height each mouse-scroll notch changes, so close zooms move in small steps.")]
        [Range(0.01f, 0.5f)]
        public float fraction = 0.15f;

        [Tooltip("Closest zoom (minimum camera height above the ground at the focus).")]
        public float min = 10f;

        [Tooltip("Farthest zoom (maximum camera height above the ground at the focus).")]
        public float max = 50f;

        [Tooltip("Seconds the camera takes to ease to a new zoom height; 0 jumps at once.")]
        [Min(0f)]
        public float smoothTime = 0.12f;

        [Tooltip("Degrees down from the horizon at the closest zoom.")]
        [Range(1f, 90f)]
        public float pitchAtMin = 44f;

        [Tooltip("Degrees down from the horizon at the farthest zoom; pitch runs linearly with height in between.")]
        [Range(1f, 90f)]
        public float pitchAtMax = 60f;

        /// <summary>Pitch in degrees down from the horizon at <paramref name="height"/>, linear between the zoom limits.</summary>
        public float PitchAt(float height) => Mathf.Lerp(pitchAtMin, pitchAtMax, Mathf.InverseLerp(min, max, height));

        /// <summary>The height after one scroll notch: scrolling up zooms in by <see cref="fraction"/> of it, within the limits.</summary>
        public float Zoomed(float height, float scroll) => Clamp(height * (1f - Mathf.Sign(scroll) * fraction));

        public float Clamp(float height) => Mathf.Clamp(height, min, max);
    }
}
