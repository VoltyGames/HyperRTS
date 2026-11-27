using System.Collections.Generic;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Nav;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Hints;

/// <summary>What the AI hints are computed from: the nav grid, the bases and the things the bots care about.</summary>
public sealed class MapHintInput(MapNavGrid nav, IReadOnlyList<MapSpawn> spawns,
    IReadOnlyList<(Vector2 Position, int Owner)> nodes, IReadOnlyList<(Vector2 Position, float Radius)> crossings,
    float baseRadius, float corridorWidth)
{
    public MapNavGrid Nav { get; } = nav;

    /// <summary>Slots with their bases and teams.</summary>
    public IReadOnlyList<MapSpawn> Spawns { get; } = spawns;

    /// <summary>Resource nodes: where they stand and the slot that starts owning them (0 for none).</summary>
    public IReadOnlyList<(Vector2 Position, int Owner)> Nodes { get; } = nodes;

    /// <summary>Bridges and fords: their middle and how far from it a path counts as crossing.</summary>
    public IReadOnlyList<(Vector2 Position, float Radius)> Crossings { get; } = crossings;

    public float BaseRadius { get; } = baseRadius;

    /// <summary>Width of a vehicle column: distinct lanes are at least this far apart, narrower ground is a choke.</summary>
    public float CorridorWidth { get; } = corridorWidth;
}
