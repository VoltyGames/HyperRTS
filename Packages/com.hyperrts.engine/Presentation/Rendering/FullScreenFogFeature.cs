using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Base of the full-screen fog features: builds the fog shader's material and the pass that draws it.</summary>
    public abstract class FullScreenFogFeature<TPass> : ScriptableRendererFeature where TPass : ScriptableRenderPass
    {
        [Tooltip("The feature's Hidden/HyperRTS fog shader.")]
        public Shader shader;

        private Material material;

        protected TPass Pass { get; private set; }

        protected abstract string RequiredShaderName { get; }

        public override void Create()
        {
            if (shader == null)
            {
                Debug.LogError($"HyperRTS: {GetType().Name} '{name}' has no shader; set it to {RequiredShaderName}.", this);
                return;
            }

            material = CoreUtils.CreateEngineMaterial(shader);
            Pass = CreatePass(material);
        }

        protected abstract TPass CreatePass(Material material);

        // Orthographic cameras are top-down captures such as the minimap.
        protected static bool IsTopDown(Camera camera) => camera.orthographic;

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            Pass = null;
        }
    }
}
