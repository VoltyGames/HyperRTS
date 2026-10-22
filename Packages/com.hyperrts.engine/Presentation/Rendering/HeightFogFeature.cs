using HyperRTS.Presentation.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Draws the Volume-blended <see cref="HeightFog"/> in game and scene view perspective cameras.</summary>
    public sealed class HeightFogFeature : FullScreenFogFeature<HeightFogPass>
    {
        public const string ShaderName = "Hidden/HyperRTS/HeightFog";

        protected override string RequiredShaderName => ShaderName;

        protected override HeightFogPass CreatePass(Material material) => new(material);

        // Every camera gets the pass, so one that draws no fog still clears it for its transparents.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Pass == null)
            {
                return;
            }

            var cameraData = renderingData.cameraData;
            var fogged = cameraData.cameraType == CameraType.Game || cameraData.cameraType == CameraType.SceneView;
            var fog = VolumeManager.instance.stack.GetComponent<HeightFog>();
            Pass.Setup(fog, fog.IsActive() && fogged && !IsTopDown(cameraData.camera));
            renderer.EnqueuePass(Pass);
        }
    }
}
