using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library;

/// <summary>
/// Marks a string field that names a map library item by key, so editors offer a picker of the items of
/// <see cref="Types"/> (library item classes such as <c>MapClimateSurface</c>; several when either fits).
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class MapKeyAttribute(params Type[] types) : PropertyAttribute
{
    public Type[] Types { get; } = types;

    /// <summary>A spec may leave the key empty for "none" (a ford's bridge prop).</summary>
    public bool Optional { get; set; }
}
