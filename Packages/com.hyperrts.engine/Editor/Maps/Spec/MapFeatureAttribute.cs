using System;
using System.Reflection;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Spec;

/// <summary>
/// Marks a spec feature class with the name issues call it by ("wood 2") and how the Map Editor draws it. Its
/// geometry comes from <see cref="IMapPoint"/> or <see cref="IMapLine"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MapFeatureAttribute(string name, int order, float red, float green, float blue) : Attribute
{
    public string Name { get; } = name;

    /// <summary>The Map Editor draws features of a higher order over those of a lower one.</summary>
    public int Order { get; } = order;

    public Color Color { get; } = new Color(red, green, blue);

    /// <summary>Lines only: the points close into a polygon.</summary>
    public bool Closed { get; set; }

    /// <summary>Points only: the float field drawn and dragged as a circle around the point.</summary>
    public string Radius { get; set; }

    public static MapFeatureAttribute Require(Type type) => type.GetCustomAttribute<MapFeatureAttribute>()
        ?? throw new ArgumentException($"HyperRTS: {type.Name} is not a map feature class; give it [MapFeature].", nameof(type));
}
