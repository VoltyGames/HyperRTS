using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Client.Interaction
{
    /// <summary>Written by the cursor intent system each frame, read by the cursor display.</summary>
    public struct CursorIntent : IComponentData
    {
        public const int HighestPrecedence = 8;

        public CursorKind Kind;

        /// <summary>The scroll cursor pointing along an edge direction from <see cref="ScrollEdge"/>.</summary>
        public static CursorKind Scroll(int2 edge) => (edge.x, edge.y) switch
        {
            (< 0, > 0) => CursorKind.ScrollUpLeft,
            (> 0, > 0) => CursorKind.ScrollUpRight,
            (_, > 0) => CursorKind.ScrollUp,
            (< 0, < 0) => CursorKind.ScrollDownLeft,
            (> 0, < 0) => CursorKind.ScrollDownRight,
            (_, < 0) => CursorKind.ScrollDown,
            (< 0, _) => CursorKind.ScrollLeft,
            _ => CursorKind.ScrollRight,
        };

        /// <summary>The cursor of a targeted command waiting for its click, such as attack-move from a hotkey.</summary>
        public static CursorKind ForPending(CommandType command) => command switch
        {
            CommandType.Move => CursorKind.Default,
            CommandType.AttackMove or CommandType.Patrol => CursorKind.AttackMove,
            CommandType.Attack => CursorKind.Attack,
            CommandType.Gather => CursorKind.Gather,
            CommandType.Build => CursorKind.Build,
            CommandType.SetRallyPoint => CursorKind.Rally,
            CommandType.Repair => CursorKind.Repair,
            CommandType.Capture => CursorKind.Capture,
            CommandType.Enter => CursorKind.Garrison,
            CommandType.Unload => CursorKind.Unload,
            _ => CursorKind.Target,
        };

        /// <summary>The cursor of the order a right-click would give one unit.</summary>
        public static CursorKind ForOrder(OrderType order) => order switch
        {
            OrderType.None => CursorKind.Invalid,
            OrderType.Move => CursorKind.Default,
            OrderType.AttackMove or OrderType.Patrol => CursorKind.AttackMove,
            OrderType.Attack => CursorKind.Attack,
            OrderType.Gather => CursorKind.Gather,
            OrderType.Build => CursorKind.Build,
            OrderType.Repair => CursorKind.Repair,
            OrderType.Capture => CursorKind.Capture,
            OrderType.Enter or OrderType.ReturnToBase => CursorKind.Garrison,
            _ => CursorKind.Target,
        };

        /// <summary>When units of a mixed selection would act differently, the cursor of the highest one shows.</summary>
        public static int Precedence(CursorKind kind) => kind switch
        {
            CursorKind.Attack => HighestPrecedence,
            CursorKind.Capture => 7,
            CursorKind.Garrison => 6,
            CursorKind.Repair => 5,
            CursorKind.Gather => 4,
            CursorKind.Build => 3,
            CursorKind.Target => 2,
            CursorKind.AttackMove => 1,
            _ => -1,
        };
    }
}
