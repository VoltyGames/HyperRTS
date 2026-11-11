using System;
using UnityEngine;

namespace HyperRTS.Input.Cameras
{
    /// <summary>What the camera tells tree shaders so trees in front of the ground it looks at dither away at close zooms.</summary>
    [Serializable]
    public class CameraTreeFade
    {
        private static readonly int CameraId = Shader.PropertyToID("_HyperRTSFadeCamera");
        private static readonly int FocusId = Shader.PropertyToID("_HyperRTSFadeFocus");
        private static readonly int ReachId = Shader.PropertyToID("_HyperRTSFadeReach");
        private static readonly int AmountId = Shader.PropertyToID("_HyperRTSFadeAmount");

        [Tooltip("Camera height at or below which trees in front of the view fade fully.")]
        [Min(0f)]
        public float fullBelowHeight = 6f;

        [Tooltip("Camera height at or above which trees never fade; the fade eases in between the two heights.")]
        [Min(0f)]
        public float noneAboveHeight = 14f;

        [Tooltip("Metres around the camera inside which trees are fully faded; they ease back in over the outer half.")]
        [Min(0f)]
        public float cameraRadius = 4f;

        [Tooltip("Metres either side of the line from the camera to the focus inside which trees are fully faded; " +
                 "they ease back in over the outer half.")]
        [Min(0f)]
        public float lineRadius = 4f;

        [Tooltip("Metres the faded line runs on past the focus, so trees standing over the ground in view fade too.")]
        [Min(0f)]
        public float beyondFocus = 6f;

        /// <summary>How much trees fade at a camera <paramref name="height"/>: 1 at or below <see cref="fullBelowHeight"/>, 0 at or above <see cref="noneAboveHeight"/>.</summary>
        public float Amount(float height) =>
            1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fullBelowHeight, noneAboveHeight, height));

        public void Share(Vector3 camera, Vector3 focus, float height)
        {
            Shader.SetGlobalVector(CameraId, new Vector4(camera.x, camera.y, camera.z, cameraRadius));
            Shader.SetGlobalVector(FocusId, new Vector4(focus.x, focus.y, focus.z, lineRadius));
            Shader.SetGlobalFloat(ReachId, beyondFocus);
            Shader.SetGlobalFloat(AmountId, Amount(height));
        }

        public static void Stop() => Shader.SetGlobalFloat(AmountId, 0f);
    }
}
