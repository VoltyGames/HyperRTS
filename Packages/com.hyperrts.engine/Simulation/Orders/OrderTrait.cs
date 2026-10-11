using System;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Behaviours an order type can opt into; see <see cref="OrderTraits.Register"/>.</summary>
    [Flags]
    public enum OrderTrait : byte
    {
        None = 0,

        /// <summary>Combat auto-acquires targets and fights on the way.</summary>
        EngagesWhileMoving = 1 << 0,

        /// <summary>Heads for <see cref="Order.Position"/> and returns to it after each fight.</summary>
        AttackMove = 1 << 1,

        /// <summary>Walks to <see cref="Order.Position"/>: groups spread into a formation and the move order runs it.</summary>
        UsesFormation = 1 << 2,
    }
}
