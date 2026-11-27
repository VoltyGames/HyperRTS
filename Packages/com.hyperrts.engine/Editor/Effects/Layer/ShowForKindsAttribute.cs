using UnityEngine;

namespace HyperRTS.Editor.Effects.Layer;

/// <summary>Shows a layer's settings group in the Inspector only while the layer's kind is one of these.</summary>
public sealed class ShowForKindsAttribute(params EffectLayerKind[] kinds) : PropertyAttribute
{
    public readonly EffectLayerKind[] Kinds = kinds;
}
