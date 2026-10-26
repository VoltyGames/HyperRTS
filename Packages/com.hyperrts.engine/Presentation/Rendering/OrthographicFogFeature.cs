using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Keeps URP's fog off orthographic cameras such as the minimap capture, which URP's fog feature skips without
    /// resetting the last perspective camera's fog.
    /// </summary>
    public sealed class OrthographicFogFeature : ScriptableRendererFeature
    {
        private ClearAtmosphereFogPass pass;

        public override void Create() => pass = new ClearAtmosphereFogPass();

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.camera.orthographic)
            {
                renderer.EnqueuePass(pass);
            }
        }

        protected override void Dispose(bool disposing) => pass = null;
    }
}
