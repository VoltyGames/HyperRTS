using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering.TeamOutline
{
    /// <summary>
    /// A thin line in the owner's colour round every owned unit and building on screen, drawn into the finished frame
    /// after upscaling, so it stays crisp and still at any render scale.
    /// </summary>
    public sealed class TeamOutlineFeature : ScriptableRendererFeature
    {
        /// <summary>The LightMode of LitTeam's pass that draws a unit into the outline mask.</summary>
        public const string UnitPass = "HyperRTSUnitOutline";

        /// <summary>The LightMode of LitTeam's pass that draws a building into the outline mask.</summary>
        public const string BuildingPass = "HyperRTSBuildingOutline";

        private const float ReferenceHeight = 1080f;

        [Tooltip("Line width in pixels on a 1080-pixel-high screen; taller screens scale it up. 0.5 to 4.")]
        public float width = 1.5f;

        [Tooltip("How opaque the line is, 0 to 1.")]
        public float opacity = 0.8f;

        [Tooltip("Widest gap inside a building, in pixels on a 1080-pixel-high screen, that its outline bridges, so " +
                 "lattice masts, fences and rails read as one shape. 0 to 24.")]
        public float buildingGap = 12f;

        private Material _material;
        private TeamOutlinePass _pass;

        /// <summary>Why the settings can't draw an outline, or empty when they can.</summary>
        public string SettingsProblem()
        {
            if (width is < 0.5f or > 4f)
            {
                return $"Team outline width {width} must be 0.5 to 4 pixels.";
            }

            if (opacity is < 0f or > 1f)
            {
                return $"Team outline opacity {opacity} must be 0 to 1.";
            }

            return buildingGap is < 0f or > 24f ? $"Team outline building gap {buildingGap} must be 0 to 24 pixels." : string.Empty;
        }

        /// <summary>Converts pixels on a 1080-pixel-high screen to pixels on one <paramref name="screenHeight"/> high.</summary>
        public static float Scaled(float pixels, int screenHeight) => pixels * screenHeight / ReferenceHeight;

        public override void Create()
        {
            _material = CoreUtils.CreateEngineMaterial(EngineShaders.Get().TeamOutline);
            _pass = new TeamOutlinePass(_material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var shown = SceneCameras.Shown(renderingData.cameraData.cameraType);
            if (!shown || opacity <= 0f || TeamOutlines.Hidden)
            {
                return;
            }

            var camera = renderingData.cameraData.camera;
            var screen = new Vector2Int(camera.pixelWidth, camera.pixelHeight);
            _pass.Setup(Scaled(width, screen.y), opacity, Scaled(buildingGap, screen.y), screen);
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
