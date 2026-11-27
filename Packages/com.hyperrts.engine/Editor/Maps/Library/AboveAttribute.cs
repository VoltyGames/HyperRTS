using System;

namespace HyperRTS.Editor.Maps.Library;

/// <summary>Marks a number field of a library item or spec that must be above <see cref="Limit"/>; the map checks report it.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class AboveAttribute(float limit) : Attribute
{
    public float Limit { get; } = limit;
}
