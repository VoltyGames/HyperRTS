using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Orders;
using Unity.Mathematics;

namespace HyperRTS.Input.Commands;

/// <summary>A move button held down on the ground: the order waits for release, and a drag aims its facing.</summary>
public struct FacingDrag
{
    /// <summary>Pointer travel from the press, in screen pixels, past which release issues a faced move.</summary>
    public const float ThresholdPixels = 6f;

    public CommandType Type;
    public int Argument;
    public bool Queue;
    public float2 PressScreen;
    public float3 Press;

    /// <summary>Last ground point under the pointer, kept while the pointer is off the terrain.</summary>
    public float3 Pointer;

    public readonly bool Held => Type != CommandType.None;

    /// <summary>Only moves to a ground point wait for release; targeted commands issue on press.</summary>
    public static bool CanStart(CommandType type) =>
        type is CommandType.Smart or CommandType.Move or CommandType.AttackMove;

    public static bool IsAimed(float2 pressScreen, float2 screen) =>
        math.distance(pressScreen, screen) > ThresholdPixels;

    public static FacingDrag Start(CommandType type, int argument, bool queue, float2 screen, float3 ground) => new()
    {
        Type = type, Argument = argument, Queue = queue, PressScreen = screen, Press = ground, Pointer = ground,
    };

    public readonly FacingDragState State(float2 screen) => new()
    {
        Aimed = IsAimed(PressScreen, screen), Type = Type, Press = Press, Pointer = Pointer,
    };

    /// <summary>The order release issues: a plain move below the threshold, a faced one past it.</summary>
    public readonly PlayerCommand Command(in FacingDragState state) => new()
    {
        Type = Type, Position = Press, Queue = Queue, Argument = Argument, Faced = state.Aimed,
        Yaw = state.Aimed ? state.Yaw : 0f,
    };
}
