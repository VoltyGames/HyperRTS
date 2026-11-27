using System;

namespace HyperRTS.Simulation.Common.Authoring;

/// <summary>
/// Draws the method as a button under the authoring inspector's fields; each click is one undo step. Put it on a
/// parameterless method of the component, or on a static editor method that takes the component.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class InspectorButtonAttribute(string label) : Attribute
{
    public string Label { get; } = label;

    /// <summary>Name of a glyph (such as "wand-sparkles") drawn left of the label; empty for none.</summary>
    public string Glyph { get; set; } = "";
}
