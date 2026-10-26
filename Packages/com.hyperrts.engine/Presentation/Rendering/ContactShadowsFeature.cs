using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Short, sharp sun shadows where small things meet the ground: traces the depth buffer toward the sun and darkens
    /// URP's screen-space shadow texture, so every lit shader gets them. Needs URP's Screen Space Shadows feature and a
    /// forward renderer.
    /// </summary>
    public sealed class ContactShadowsFeature : ScriptableRendererFeature
    {
        public const string ShaderName = "Hidden/HyperRTS/ContactShadows";

        [Tooltip("The Hidden/HyperRTS/ContactShadows shader.")]
        public Shader shader;

        [Tooltip("Metres traced toward the sun from each pixel.")]
        public float length = 1f;

        [Tooltip("Depth samples along the trace.")]
        public int steps = 12;

        [Tooltip("Metres an occluder in the depth buffer is taken to extend behind its surface.")]
        public float thickness = 0.4f;

        [Tooltip("How dark a contact shadow is, 0 to 1, scaled by the sun's shadow strength.")]
        public float strength = 1f;

        [Tooltip("Lowest quality level index the shadows run at, so cheaper levels skip them.")]
        public int lowestQualityLevel = 1;

        private Material material;
        private ContactShadowsPass pass;

        /// <summary>Why the settings can't trace, or empty when they can.</summary>
        public string SettingsProblem()
        {
            if (length <= 0f)
            {
                return $"Contact shadow length {length} must be above 0.";
            }

            if (steps < 1)
            {
                return $"Contact shadow steps {steps} must be at least 1.";
            }

            if (thickness <= 0f)
            {
                return $"Contact shadow thickness {thickness} must be above 0.";
            }

            return strength is < 0f or > 1f ? $"Contact shadow strength {strength} must be 0 to 1." : string.Empty;
        }

        public bool RunsAt(int qualityLevel) => qualityLevel >= lowestQualityLevel;

        public override void Create()
        {
            if (shader == null)
            {
                Debug.LogError($"HyperRTS: {nameof(ContactShadowsFeature)} '{name}' has no shader; set it to {ShaderName}.", this);
                return;
            }

            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new ContactShadowsPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Traces(ref renderingData))
            {
                return;
            }

            var sun = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex];
            pass.Setup(this, sun);
            renderer.EnqueuePass(pass);
        }

        // Mirrors when URP's Screen Space Shadows pass runs, since the trace darkens its texture.
        private bool Traces(ref RenderingData renderingData)
        {
            if (pass == null || !RunsAt(QualitySettings.GetQualityLevel()))
            {
                return false;
            }

            var cameraData = renderingData.cameraData;
            var perspective = !cameraData.camera.orthographic;
            var shown = cameraData.cameraType == CameraType.Game || cameraData.cameraType == CameraType.SceneView;
            if (!perspective || !shown)
            {
                return false;
            }

            return renderingData.shadowData.supportsMainLightShadows && renderingData.lightData.mainLightIndex != -1;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }
    }
}
