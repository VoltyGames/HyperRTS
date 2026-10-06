namespace HyperRTS.Simulation.Orders
{
    public static class OrderTypeExtensions
    {
        /// <summary>True for orders that let combat auto-acquire targets and fight on the way.</summary>
        public static bool EngagesWhileMoving(this OrderType type) =>
            type is OrderType.AttackMove or OrderType.Patrol or OrderType.Escort;

        /// <summary>Attack-moves toward <see cref="Order.Position"/>, which units head back to after each fight.</summary>
        public static bool IsAttackMove(this OrderType type) => type is OrderType.AttackMove or OrderType.Patrol;
    }
}
