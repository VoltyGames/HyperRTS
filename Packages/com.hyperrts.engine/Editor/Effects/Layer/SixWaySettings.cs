using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Layer;

/// <summary>What a SixWay or Ring layer feeds its six-way material: its maps, density, ambient and heat emission.</summary>
[Serializable]
public sealed class SixWaySettings
{
    public Texture2D negativeTexture;
    public float opacity;
    public float ambient;
    public Texture2D emissionRamp;

    [ColorUsage(true, true)] public Color emissionColor;
    public AnimationCurve heatOverLife = new();

    public AnimationCurve minHeatOverLife = new();
}
