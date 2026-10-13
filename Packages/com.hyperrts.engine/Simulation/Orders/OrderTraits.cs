using Unity.Burst;
using Unity.Collections;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// What each order type does while it runs: the built-in types' fixed traits, plus whatever games register for
    /// their own types (once, from a system's <c>OnCreate</c>) so the engine treats them like its own.
    /// </summary>
    public static class OrderTraits
    {
        private const int TypeCount = byte.MaxValue + 1;

        private static readonly SharedStatic<Table> Registered = SharedStatic<Table>.GetOrCreate<Table, Key>();

        /// <summary>Adds <paramref name="traits"/> to <paramref name="type"/>; registering again is harmless.</summary>
        public static void Register(OrderType type, OrderTrait traits)
        {
            ref var registered = ref Registered.Data.Traits;
            if (registered.Length == 0)
            {
                // The shared table starts zeroed, so this only exposes empty entries.
                registered.Length = TypeCount;
            }

            registered[(byte)type] |= (byte)traits;
        }

        internal static bool Has(OrderType type, OrderTrait trait) => (Traits(type) & trait) != 0;

        private static OrderTrait Traits(OrderType type)
        {
            ref var registered = ref Registered.Data.Traits;
            var custom = registered.Length == 0 ? OrderTrait.None : (OrderTrait)registered[(byte)type];
            return BuiltIn(type) | custom;
        }

        private static OrderTrait BuiltIn(OrderType type) => type switch
        {
            OrderType.Move => OrderTrait.UsesFormation,
            OrderType.AttackMove or OrderType.Patrol =>
                OrderTrait.EngagesWhileMoving | OrderTrait.AttackMove | OrderTrait.UsesFormation,
            OrderType.Escort => OrderTrait.EngagesWhileMoving,
            _ => OrderTrait.None,
        };

        private struct Table
        {
            /// <summary>Registered traits by order type value; empty until the first registration.</summary>
            public FixedList512Bytes<byte> Traits;
        }

        private sealed class Key
        {
        }
    }
}
