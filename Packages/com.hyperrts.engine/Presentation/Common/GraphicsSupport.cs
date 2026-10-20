using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Common
{
    /// <summary>
    /// Whether this player can present at all: a server or headless client (<c>-nographics</c>) has no graphics device,
    /// so shaders, materials and compute kernels never load and presentation stays off
    /// (<see cref="PresentationBehaviour"/>, <see cref="HeadlessModeSystem"/>).
    /// </summary>
    public static class GraphicsSupport
    {
        public static bool Available => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
    }
}
