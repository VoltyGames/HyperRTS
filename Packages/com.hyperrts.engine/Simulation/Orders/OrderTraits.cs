using System;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>What an order does while it runs, so systems read behaviour from data, not from its type.</summary>
    [Flags]
    public enum OrderTraits : byte
    {
        None = 0,

        /// <summary>Combat auto-acquires targets and fights on the way.</summary>
        EngagesWhileMoving = 1,

        /// <summary>Attack-moves toward <see cref="Order.Position"/>, which units head back to after each fight.</summary>
        AttackMove = 2,

        /// <summary>Walks to <see cref="Order.Position"/>: groups spread into a formation and the move order runs it.</summary>
        Formation = 4,
    }
}
