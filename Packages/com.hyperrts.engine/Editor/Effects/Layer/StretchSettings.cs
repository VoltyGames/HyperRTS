using System;

namespace HyperRTS.Editor.Effects.Layer;

/// <summary>How far a Spark layer's quads stretch along their velocity.</summary>
[Serializable]
public sealed class StretchSettings
{
    public float length;
    public float lengthPerSpeed;
}
