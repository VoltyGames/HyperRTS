using HyperRTS.Presentation.Fog;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Draws the <see cref="FogOfWarRenderer"/> grid full screen in game cameras while a match shows it, and keeps URP's
    /// fog off orthographic cameras, which URP's fog feature skips without resetting the last perspective camera's fog.
    /// </summary>
    public sealed class FogOfWarFeature : ScriptableRendererFeature
    {
        public const string ShaderName = "Hidden/HyperRTS/FogOfWar";

        [Tooltip("The Hidden/HyperRTS/FogOfWar shader.")]
        public Shader shader;

        private Material material;
        private FogOfWarPass pass;
        private ClearAtmosphereFogPass clearFog;

        public override void Create()
        {
            clearFog = new ClearAtmosphereFogPass();
            if (shader == null)
            {
                Debug.LogError($"HyperRTS: {nameof(FogOfWarFeature)} '{name}' has no shader; set it to {ShaderName}.", this);
                return;
            }

            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new FogOfWarPass(material);
        }

        // Orthographic cameras are top-down captures such as the minimap: neither fog draws there.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.camera.orthographic)
            {
                renderer.EnqueuePass(clearFog);
                return;
            }

            if (pass == null || cameraData.cameraType != CameraType.Game || !FogOfWarRenderer.Shown)
            {
                return;
            }

            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
            clearFog = null;
        }
    }
}
