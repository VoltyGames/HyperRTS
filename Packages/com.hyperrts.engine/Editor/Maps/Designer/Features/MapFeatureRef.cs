using HyperRTS.Editor.Maps.Designer.Window;

namespace HyperRTS.Editor.Maps.Designer.Features;

/// <summary>One feature of a spec: its kind's list and its index in that list.</summary>
public readonly record struct MapFeatureRef(MapFeatureKind Kind, int Index)
{
    public static MapFeatureRef None => default;

    public bool IsNone => Kind == null;

    /// <summary>The feature's property path on a <see cref="MapSpecDocument"/>.</summary>
    public string PropertyPath => MapSpecDocument.ElementPath(Kind.Field, Index);

    public override string ToString() => IsNone ? "none" : $"{Kind.Label} {Index + 1}";
}
