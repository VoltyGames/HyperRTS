using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Interaction;

/// <summary>A held command drag on the ground that aims a move's facing. Written by input, read by the facing preview.</summary>
public struct FacingDragState : IComponentData
{
    /// <summary>The pointer has left the press point by more than the drag threshold, so release issues a faced move.</summary>
    public bool Aimed;

    /// <summary>The command the release issues: Smart, Move or AttackMove.</summary>
    public CommandType Type;

    /// <summary>Ground point the button went down on, where the units go.</summary>
    public float3 Press;

    /// <summary>Ground point under the pointer now.</summary>
    public float3 Pointer;

    /// <summary>Unit XZ direction from the press point to the pointer, as the move will face.</summary>
    public readonly float2 Facing => math.normalizesafe(Pointer.xz - Press.xz);

    /// <summary>The facing as <see cref="PlayerCommand.Yaw"/>.</summary>
    public readonly float Yaw => math.atan2(Facing.x, Facing.y);
}
