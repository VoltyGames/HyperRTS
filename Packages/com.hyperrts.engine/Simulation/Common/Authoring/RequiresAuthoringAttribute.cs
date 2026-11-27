using System;

namespace HyperRTS.Simulation.Common.Authoring;

/// <summary>
/// An authoring component this one needs on the same object. Unlike <c>[RequireComponent]</c> it only warns,
/// so an abstract base such as <c>GameEntityAuthoring</c> can be required.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresAuthoringAttribute(Type type, string description) : Attribute
{
    public Type Type { get; } = type;

    /// <summary>How the warning names the requirement, e.g. "a Building".</summary>
    public string Description { get; } = description;
}
