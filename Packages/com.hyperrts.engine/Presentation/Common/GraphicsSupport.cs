using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Common
{
    /// <summary>
    /// Whether this player has a graphics device to present with; a server or <c>-nographics</c> client has none.
    /// </summary>
    public static class GraphicsSupport
    {
        public static bool Available => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
    }
}
