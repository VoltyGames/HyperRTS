using HyperRTS.Presentation.Fog;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Draws the <see cref="FogOfWarRenderer"/> grid full screen in game cameras while a match shows it.</summary>
    public sealed class FogOfWarFeature : ScriptableRendererFeature
    {
        public const string ShaderName = "Hidden/HyperRTS/FogOfWar";

        [Tooltip("The Hidden/HyperRTS/FogOfWar shader.")]
        public Shader shader;

        private Material material;
        private FogOfWarPass pass;

        public override void Create()
        {
            if (shader == null)
            {
                Debug.LogError($"HyperRTS: Fog Of War feature '{name}' has no shader; set it to {ShaderName}.", this);
                return;
            }

            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new FogOfWarPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Orthographic cameras are top-down captures such as the minimap.
            var cameraData = renderingData.cameraData;
            var drawn = cameraData.cameraType == CameraType.Game && !cameraData.camera.orthographic;
            if (pass == null || !drawn || !FogOfWarRenderer.Shown)
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
        }
    }
}
