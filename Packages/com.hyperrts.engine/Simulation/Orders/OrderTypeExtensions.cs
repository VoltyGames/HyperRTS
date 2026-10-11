namespace HyperRTS.Simulation.Orders
{
    /// <summary>Order type traits from <see cref="OrderTraits"/>: the built-in types', plus whatever games registered.</summary>
    public static class OrderTypeExtensions
    {
        /// <summary>True for orders that let combat auto-acquire targets and fight on the way.</summary>
        public static bool EngagesWhileMoving(this OrderType type) => OrderTraits.Has(type, OrderTrait.EngagesWhileMoving);

        /// <summary>Attack-moves toward <see cref="Order.Position"/>, which units head back to after each fight.</summary>
        public static bool IsAttackMove(this OrderType type) => OrderTraits.Has(type, OrderTrait.AttackMove);

        /// <summary>Walks to <see cref="Order.Position"/>: groups spread into a formation and the move order runs it.</summary>
        public static bool UsesFormation(this OrderType type) => OrderTraits.Has(type, OrderTrait.UsesFormation);
    }
}
