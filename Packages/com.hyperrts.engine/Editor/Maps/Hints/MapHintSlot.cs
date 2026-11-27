using System.Collections.Generic;
using HyperRTS.Editor.Maps.Layout;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Hints;

/// <summary>One slot's base while the hints are computed: where it is on the nav grid and how far everything is to walk.</summary>
public sealed class MapHintSlot(MapSpawn spawn, int2 cell, int cellIndex, float[] walk)
{
    public int Slot { get; } = spawn.Slot;

    public int Team { get; } = spawn.Team;

    public Vector2 Position { get; } = spawn.Position;

    /// <summary>The walkable cell nearest the base.</summary>
    public int2 Cell { get; } = cell;

    public int CellIndex { get; } = cellIndex;

    /// <summary>Metres to walk from the base to each nav cell by index; infinity where unreachable.</summary>
    public float[] Walk { get; } = walk;

    public List<Vector2> Expansions { get; } = [];

    public List<Vector2> Defence { get; } = [];
}
