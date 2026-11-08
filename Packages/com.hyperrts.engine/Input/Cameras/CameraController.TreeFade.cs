using UnityEngine;

namespace HyperRTS.Input.Cameras
{
    /// <summary>What the camera tells tree shaders so trees in front of the ground it looks at dither away at close zooms.</summary>
    public partial class CameraController
    {
        private static readonly int FadeCameraId = Shader.PropertyToID("_HyperRTSFadeCamera");
        private static readonly int FadeFocusId = Shader.PropertyToID("_HyperRTSFadeFocus");
        private static readonly int FadeReachId = Shader.PropertyToID("_HyperRTSFadeReach");
        private static readonly int FadeAmountId = Shader.PropertyToID("_HyperRTSFadeAmount");

        [Header("Tree Fade")]
        [Tooltip("Camera height at or below which trees in front of the view fade fully.")]
        [Min(0f)]
        public float treeFadeFullBelowHeight = 6f;

        [Tooltip("Camera height at or above which trees never fade; the fade eases in between the two heights.")]
        [Min(0f)]
        public float treeFadeNoneAboveHeight = 14f;

        [Tooltip("Metres around the camera inside which trees are fully faded; they ease back in over the outer half.")]
        [Min(0f)]
        public float treeFadeCameraRadius = 4f;

        [Tooltip("Metres either side of the line from the camera to the focus inside which trees are fully faded; " +
                 "they ease back in over the outer half.")]
        [Min(0f)]
        public float treeFadeLineRadius = 4f;

        [Tooltip("Metres the faded line runs on past the focus, so trees standing over the ground in view fade too.")]
        [Min(0f)]
        public float treeFadeBeyondFocus = 6f;

        /// <summary>How much trees fade at a camera <paramref name="height"/>: 1 at or below <paramref name="fullBelow"/>, 0 at or above <paramref name="noneAbove"/>.</summary>
        public static float TreeFadeAmount(float height, float fullBelow, float noneAbove) =>
            1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fullBelow, noneAbove, height));

        private void ShareTreeFade()
        {
            Shader.SetGlobalVector(FadeCameraId, new Vector4(transform.position.x, transform.position.y,
                transform.position.z, treeFadeCameraRadius));
            Shader.SetGlobalVector(FadeFocusId, new Vector4(_focus.x, _focus.y, _focus.z, treeFadeLineRadius));
            Shader.SetGlobalFloat(FadeReachId, treeFadeBeyondFocus);
            Shader.SetGlobalFloat(FadeAmountId, TreeFadeAmount(_height, treeFadeFullBelowHeight, treeFadeNoneAboveHeight));
        }

        private static void StopTreeFade() => Shader.SetGlobalFloat(FadeAmountId, 0f);
    }
}
