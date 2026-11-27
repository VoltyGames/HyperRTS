using UnityEngine;

namespace HyperRTS.Editor.Maps.Layout;

/// <summary>A player start in map space with the slot and team the symmetry gave it.</summary>
public sealed class MapSpawn(Vector2 position, float yaw, int slot, int team, int copy)
{
    public Vector2 Position { get; } = position;

    public float Yaw { get; } = yaw;

    /// <summary>Player slot from 1, the owner number of the start kit's owner.</summary>
    public int Slot { get; } = slot;

    public int Team { get; } = team;

    public int Copy { get; } = copy;
}
