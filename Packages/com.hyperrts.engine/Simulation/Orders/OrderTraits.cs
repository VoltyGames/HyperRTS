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

        /// <summary>A group given the order spreads into formation slots around <see cref="Order.Position"/>.</summary>
        Formation = 4,

        /// <summary><see cref="MoveOrderSystem"/> takes the unit to <see cref="Order.Position"/> and finishes it there.</summary>
        MovesToPosition = 8,

        /// <summary>A finished order goes to the back of the queue, so the unit loops through its legs.</summary>
        Loops = 16,
    }
}
