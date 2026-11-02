using UnityEngine;

namespace HyperRTS.Input.Cameras
{
    /// <summary>The camera's pitch with zoom height, and its tilt toward the horizon at the closest zooms.</summary>
    public partial class CameraController
    {
        [Header("Pitch")]
        [Tooltip("Degrees down from the horizon at the closest zoom; lower looks further toward the horizon.")]
        [Range(1f, 90f)]
        public float pitchAtMinZoom = 44f;

        [Tooltip("Degrees down from the horizon at the farthest zoom; pitch runs linearly with height in between.")]
        [Range(1f, 90f)]
        public float pitchAtMaxZoom = 60f;

        [Tooltip("Height below which the camera eases up toward the horizon, reaching Pitch At Tilt at the closest " +
                 "zoom; at or below the closest zoom it never tilts.")]
        [Min(0f)]
        public float tiltBelowHeight;

        [Tooltip("Degrees down from the horizon at the closest zoom when the camera tilts.")]
        [Range(1f, 90f)]
        public float pitchAtTilt = 18f;

        [Tooltip("Far clip plane at the closest zoom when the camera tilts, so the horizon shows; it eases in with " +
                 "the tilt.")]
        [Min(1f)]
        public float farClipAtTilt = 3000f;

        /// <summary>
        /// Pitch in degrees down from the horizon at <paramref name="height"/>: linear between the zoom limits, eased
        /// toward <see cref="pitchAtTilt"/> below <see cref="tiltBelowHeight"/>.
        /// </summary>
        public float PitchAt(float height) =>
            Mathf.Lerp(Mathf.Lerp(pitchAtMinZoom, pitchAtMaxZoom, Mathf.InverseLerp(minZoom, maxZoom, height)),
                pitchAtTilt, TiltAt(height));

        /// <summary>How far the camera has tilted toward the horizon at <paramref name="height"/>, 0 to 1.</summary>
        public float TiltAt(float height) =>
            tiltBelowHeight > minZoom ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tiltBelowHeight, minZoom, height)) : 0f;
    }
}
