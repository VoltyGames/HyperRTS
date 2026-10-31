using HyperRTS.Presentation.Fog;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Fog
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

        private Material _material;
        private FogOfWarPass _pass;
        private ClearAtmosphereFogPass _clearFog;

        public override void Create()
        {
            _clearFog = new ClearAtmosphereFogPass();
            if (shader == null)
            {
                Debug.LogError($"HyperRTS: {nameof(FogOfWarFeature)} '{name}' has no shader; set it to {ShaderName}.", this);
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new FogOfWarPass(_material);
        }

        // Orthographic cameras are top-down captures such as the minimap: neither fog draws there.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.camera.orthographic)
            {
                renderer.EnqueuePass(_clearFog);
                return;
            }

            if (_pass == null || cameraData.cameraType != CameraType.Game || !FogOfWarRenderer.Shown)
            {
                return;
            }

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
            _clearFog = null;
        }
    }
}
