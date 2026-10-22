using HyperRTS.Presentation.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Draws the Volume-blended <see cref="HeightFog"/> in game and scene view perspective cameras.</summary>
    public sealed class HeightFogFeature : ScriptableRendererFeature
    {
        public const string ShaderName = "Hidden/HyperRTS/HeightFog";

        [Tooltip("The Hidden/HyperRTS/HeightFog shader.")]
        public Shader shader;

        private Material material;
        private HeightFogPass pass;

        public override void Create()
        {
            if (shader == null)
            {
                Debug.LogError($"HyperRTS: Height Fog feature '{name}' has no shader; set it to {ShaderName}.", this);
                return;
            }

            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new HeightFogPass(material);
        }

        // Every camera gets the pass, so one that draws no fog still clears it for its transparents.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null)
            {
                return;
            }

            var fog = VolumeManager.instance.stack.GetComponent<HeightFog>();
            pass.Setup(fog, renderingData.cameraData.cameraType, renderingData.cameraData.camera.orthographic);
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
