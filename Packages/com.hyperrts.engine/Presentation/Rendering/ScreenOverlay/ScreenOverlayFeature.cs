using HyperRTS.Presentation.Common;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering.ScreenOverlay;

/// <summary>
/// Draws the <see cref="Pass"/> pass of visible renderers into the finished frame, after upscaling and under the UI,
/// so marks sized in screen pixels, such as health bars, stay crisp at any render scale.
/// </summary>
public sealed class ScreenOverlayFeature : ScriptableRendererFeature
{
    /// <summary>The LightMode a shader pass names to be drawn here.</summary>
    public const string Pass = "HyperRTSScreenOverlay";

    private ScreenOverlayPass _pass;

    public override void Create() => _pass = new ScreenOverlayPass();

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (SceneCameras.Shown(renderingData.cameraData.cameraType))
        {
            renderer.EnqueuePass(_pass);
        }
    }
}
