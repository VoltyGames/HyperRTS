using UnityEngine;

namespace HyperRTS.Presentation.Overlays.Markers
{
    /// <summary>One ground marker as <c>HyperRTS/Ground Marker</c> reads it.</summary>
    public struct MarkerInstance
    {
        /// <summary>Scales a unit grid to the footprint; its height is the lowest the marker may sit.</summary>
        public Matrix4x4 ObjectToWorld;

        public Vector4 Tint;

        /// <summary>Line width and fade in metres, fill opacity, corner radius in metres (negative for brackets).</summary>
        public Vector4 Shape;
    }
}
