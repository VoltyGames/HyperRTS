using Unity.Burst;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// What custom order types do while they run, so the engine treats them like its own: register a type once (from
    /// a system's <c>OnCreate</c>) and the <see cref="OrderTypeExtensions"/> checks include it.
    /// </summary>
    public static class OrderTraits
    {
        private static readonly SharedStatic<Table> Custom = SharedStatic<Table>.GetOrCreate<Table, Key>();

        /// <summary>Adds <paramref name="traits"/> to <paramref name="type"/>; registering again is harmless.</summary>
        public static void Register(OrderType type, OrderTrait traits)
        {
            ref var table = ref Custom.Data;
            if ((traits & OrderTrait.EngagesWhileMoving) != 0)
            {
                table.EngagesWhileMoving.Add((byte)type);
            }

            if ((traits & OrderTrait.AttackMove) != 0)
            {
                table.AttackMove.Add((byte)type);
            }

            if ((traits & OrderTrait.UsesFormation) != 0)
            {
                table.UsesFormation.Add((byte)type);
            }
        }

        internal static bool Has(OrderType type, OrderTrait trait)
        {
            ref var table = ref Custom.Data;
            return trait switch
            {
                OrderTrait.EngagesWhileMoving => table.EngagesWhileMoving.Contains(type),
                OrderTrait.AttackMove => table.AttackMove.Contains(type),
                OrderTrait.UsesFormation => table.UsesFormation.Contains(type),
                _ => false,
            };
        }

        private struct Table
        {
            public TypeSet EngagesWhileMoving;
            public TypeSet AttackMove;
            public TypeSet UsesFormation;
        }

        private sealed class Key
        {
        }
    }
}
