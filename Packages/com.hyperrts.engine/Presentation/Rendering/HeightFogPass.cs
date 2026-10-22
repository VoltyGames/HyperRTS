using HyperRTS.Presentation.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Publishes the camera's blended <see cref="HeightFog"/> and blends it over the opaques and sky.</summary>
    public sealed class HeightFogPass : ScriptableRenderPass
    {
        private readonly Material material;

        private sealed class PassData
        {
            public Material Material;
            public HeightFog Fog;
            public bool Drawn;
        }

        // After the skybox and screen-space decals so they fog too, and before the unit silhouettes so those stay crisp.
        public HeightFogPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 1;
            profilingSampler = new ProfilingSampler("Height Fog");
        }

        // Whether this camera draws the fog, else it only clears the globals for its transparents.
        private bool drawn;

        public void Setup(HeightFog fog, CameraType type, bool orthographic)
        {
            // Orthographic cameras are top-down captures such as the minimap.
            var fogged = type == CameraType.Game || type == CameraType.SceneView;
            drawn = fog.IsActive() && fogged && !orthographic;
            ConfigureInput(drawn ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            using var builder = renderGraph.AddRasterRenderPass<PassData>("Height Fog", out var data, profilingSampler);
            data.Material = material;
            data.Fog = VolumeManager.instance.stack.GetComponent<HeightFog>();
            data.Drawn = drawn;
            if (drawn)
            {
                builder.UseTexture(resources.cameraDepthTexture);
            }

            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) => Render(pass, context.cmd));
        }

        private static void Render(PassData pass, RasterCommandBuffer cmd)
        {
            pass.Fog.Publish(cmd, pass.Drawn);
            if (pass.Drawn)
            {
                cmd.DrawProcedural(Matrix4x4.identity, pass.Material, 0, MeshTopology.Triangles, 3);
            }
        }
    }
}
