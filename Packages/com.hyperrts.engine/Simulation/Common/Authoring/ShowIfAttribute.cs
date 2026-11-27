using System;

namespace HyperRTS.Simulation.Common.Authoring;

/// <summary>
/// Shows the field in authoring inspectors only while a sibling field is set: a bool that is true, a number above
/// 0 or an object reference that is assigned.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ShowIfAttribute(string field) : Attribute
{
    /// <summary>Name of the serialized field the condition reads, e.g. <c>nameof(splashRadius)</c>.</summary>
    public string Field { get; } = field;
}
