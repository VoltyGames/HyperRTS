using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.HUD.Map
{
    /// <summary>One minimap blip: where it sits (0 to 1, y down like UI), its fill, its size in pixels and its shape.</summary>
    public struct MinimapMarker
    {
        public float2 Point;
        public Color32 Color;
        public float Radius;
        public bool Square;
    }
}
