using System;

namespace HyperRTS.Simulation.Common.Authoring;

/// <summary>
/// A prefab the server spawns, so it must be a ghost for multiplayer clients to see it. Validation offers to
/// make it one.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class GhostPrefabAttribute(string label) : Attribute
{
    /// <summary>How a warning names the prefab, e.g. "Hull".</summary>
    public string Label { get; } = label;

    /// <summary>Checks only when the owner is itself a ghost; map scenery spawns its own on every client.</summary>
    public bool OnlyOnGhosts { get; set; }
}
