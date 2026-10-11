namespace HyperRTS.Simulation.Orders
{
    /// <summary>Order type traits: the built-in types', plus whatever games added with <see cref="OrderTraits.Register"/>.</summary>
    public static class OrderTypeExtensions
    {
        /// <summary>True for orders that let combat auto-acquire targets and fight on the way.</summary>
        public static bool EngagesWhileMoving(this OrderType type)
        {
            var builtIn = type is OrderType.AttackMove or OrderType.Patrol or OrderType.Escort;
            return builtIn || OrderTraits.Has(type, OrderTrait.EngagesWhileMoving);
        }

        /// <summary>Attack-moves toward <see cref="Order.Position"/>, which units head back to after each fight.</summary>
        public static bool IsAttackMove(this OrderType type)
        {
            var builtIn = type is OrderType.AttackMove or OrderType.Patrol;
            return builtIn || OrderTraits.Has(type, OrderTrait.AttackMove);
        }

        /// <summary>Walks to <see cref="Order.Position"/>: groups spread into a formation and the move order runs it.</summary>
        public static bool UsesFormation(this OrderType type)
        {
            var builtIn = type is OrderType.Move or OrderType.AttackMove or OrderType.Patrol;
            return builtIn || OrderTraits.Has(type, OrderTrait.UsesFormation);
        }
    }
}
