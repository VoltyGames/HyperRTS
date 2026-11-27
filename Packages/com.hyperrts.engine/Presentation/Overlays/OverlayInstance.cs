using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Overlays;

/// <summary>One flat-colour overlay instance as <c>HyperRTS/Overlay</c> reads it.</summary>
/// <remarks>An instance tinted <paramref name="color"/>, linearised as a material colour would be.</remarks>
[GenerateHLSL(needAccessors: false)]
public struct OverlayInstance(in Matrix4x4 objectToWorld, Color color)
{
    public Matrix4x4 ObjectToWorld = objectToWorld;
    public Vector4 Color = color.linear;
}
