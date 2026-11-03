using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Contrast-adaptive sharpening of the finished frame, after post-processing and any upscaler: temporal
    /// anti-aliasing and DLSS at native resolution soften ground textures and leaves, and this restores their edges
    /// without ringing on flat areas.
    /// </summary>
    public sealed class SharpenFeature : ScriptableRendererFeature
    {
        public const string ShaderName = "Hidden/HyperRTS/Sharpen";

        [Tooltip("The Hidden/HyperRTS/Sharpen shader.")]
        public Shader shader;

        [Tooltip("How strongly edges are sharpened, 0 to 1.")]
        public float strength = 0.5f;

        [Tooltip("Lowest quality level index the sharpening runs at, so cheaper levels skip it.")]
        public int lowestQualityLevel = 1;

        private Material _material;
        private SharpenPass _pass;

        /// <summary>Why the settings can't sharpen, or empty when they can.</summary>
        public string SettingsProblem() =>
            strength is < 0f or > 1f ? $"Sharpen strength {strength} must be 0 to 1." : string.Empty;

        public bool RunsAt(int qualityLevel) => qualityLevel >= lowestQualityLevel;

        public override void Create()
        {
            if (shader == null)
            {
                Debug.LogError($"HyperRTS: {nameof(SharpenFeature)} '{name}' has no shader; set it to {ShaderName}.", this);
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new SharpenPass(_material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var shown = SceneCameras.Shown(renderingData.cameraData.cameraType);
            if (_pass == null || !shown || strength <= 0f || !RunsAt(QualitySettings.GetQualityLevel()))
            {
                return;
            }

            _pass.Setup(strength);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }
    }
}
