using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>One flat-colour overlay instance as <c>HyperRTS/Overlay</c> reads it.</summary>
    [GenerateHLSL(needAccessors: false)]
    public struct OverlayInstance
    {
        public Matrix4x4 ObjectToWorld;
        public Vector4 Color;

        /// <summary>An instance tinted <paramref name="color"/>, linearised as a material colour would be.</summary>
        public OverlayInstance(in Matrix4x4 objectToWorld, Color color)
        {
            ObjectToWorld = objectToWorld;
            Color = color.linear;
        }
    }
}
