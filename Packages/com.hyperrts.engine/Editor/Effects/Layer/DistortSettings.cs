using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Layer;

/// <summary>How strongly a Distort layer bends what is behind it, and how fast its normal map slides.</summary>
[Serializable]
public sealed class DistortSettings
{
    public float distortion;
    public Vector2 scroll;
}
