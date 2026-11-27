using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Interaction;

/// <summary>
/// A move button held down on the ground: the order waits for release, and a drag aims its facing. Written by input,
/// read by the facing preview.
/// </summary>
public struct FacingDragState : IComponentData
{
    /// <summary>Pointer travel from the press, in screen pixels, past which release issues a faced move.</summary>
    public const float ThresholdPixels = 6f;

    /// <summary>The command the release issues: Smart, Move or AttackMove, or None while no drag is held.</summary>
    public CommandType Type;

    public int Argument;
    public bool Queue;
    public float2 PressScreen;

    /// <summary>Ground point the button went down on, where the units go.</summary>
    public float3 Press;

    /// <summary>Last ground point under the pointer, kept while the pointer is off the terrain.</summary>
    public float3 Pointer;

    /// <summary>The pointer has left the press point by more than the threshold, so release issues a faced move.</summary>
    public bool Aimed;

    public readonly bool Held => Type != CommandType.None;

    /// <summary>Unit XZ direction from the press point to the pointer, as the move will face.</summary>
    public readonly float2 Facing => math.normalizesafe(Pointer.xz - Press.xz);

    /// <summary>The facing as <see cref="PlayerCommand.Yaw"/>.</summary>
    public readonly float Yaw => math.atan2(Facing.x, Facing.y);

    /// <summary>Only moves to a ground point wait for release; targeted commands issue on press.</summary>
    public static bool CanStart(CommandType type) =>
        type is CommandType.Smart or CommandType.Move or CommandType.AttackMove;

    public static FacingDragState Create(CommandType type, int argument, bool queue, float2 screen, float3 ground) =>
        new() { Type = type, Argument = argument, Queue = queue, PressScreen = screen, Press = ground, Pointer = ground };

    /// <summary>Follows the pointer at <paramref name="screen"/>, which aims the move once past the threshold.</summary>
    public void Aim(float2 screen) => Aimed = math.distance(PressScreen, screen) > ThresholdPixels;

    /// <summary>The order release issues: a plain move below the threshold, a faced one past it.</summary>
    public readonly PlayerCommand Command() => new()
    {
        Type = Type, Position = Press, Queue = Queue, Argument = Argument, Faced = Aimed, Yaw = Aimed ? Yaw : 0f,
    };
}
