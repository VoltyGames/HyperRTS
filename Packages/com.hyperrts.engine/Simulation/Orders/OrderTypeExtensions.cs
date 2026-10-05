namespace HyperRTS.Simulation.Orders
{
    public static class OrderTypeExtensions
    {
        /// <summary>True for orders that let combat auto-acquire targets and fight on the way.</summary>
        public static bool EngagesWhileMoving(this OrderType type) => type == OrderType.AttackMove;
    }
}
