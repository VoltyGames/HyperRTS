namespace HyperRTS.Simulation.Orders
{
    /// <summary>What each order type does while it runs.</summary>
    public static class OrderTypeExtensions
    {
        /// <summary>True for orders that let combat auto-acquire targets and fight on the way.</summary>
        public static bool EngagesWhileMoving(this OrderType type) => type switch
        {
            OrderType.AttackMove or OrderType.Patrol or OrderType.Escort => true,
            _ => false,
        };

        /// <summary>Attack-moves toward <see cref="Order.Position"/>, which units head back to after each fight.</summary>
        public static bool IsAttackMove(this OrderType type) => type is OrderType.AttackMove or OrderType.Patrol;

        /// <summary>Walks to <see cref="Order.Position"/>: groups spread into a formation and the move order runs it.</summary>
        public static bool UsesFormation(this OrderType type) => type switch
        {
            OrderType.Move or OrderType.AttackMove or OrderType.Patrol => true,
            _ => false,
        };
    }
}
