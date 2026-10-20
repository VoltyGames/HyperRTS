using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Order kinds. Values from <see cref="GameFirst"/> up belong to games, which set their traits.</summary>
    public enum OrderType : byte
    {
        None = 0,
        Move = 1,
        AttackMove = 2,
        Attack = 3,
        Gather = 4,
        Build = 5,
        Repair = 6,
        Capture = 7,
        Enter = 8,
        UseAbility = 9,

        /// <summary>Attack-move to Position; on arrival the leg rejoins the back of the queue, so legs loop.</summary>
        Patrol = 10,

        /// <summary>Follow Target, a friendly unit, engaging hostiles on the way; ends when it dies.</summary>
        Escort = 11,

        /// <summary>Fly to the home pad (or claim one at Target, an own airfield) and dock there.</summary>
        ReturnToBase = 12,

        /// <summary>First value a game may use for its own orders; the engine never defines one from here up.</summary>
        GameFirst = 128,
    }

    /// <summary>One unit instruction: where to go and/or which entity to act on.</summary>
    public struct Order
    {
        public OrderType Type;

        /// <summary>How the order runs; <see cref="OrderWriter"/> stamps it for engine types.</summary>
        public OrderTraits Traits;

        public float3 Position;
        public Entity Target;

        /// <summary>Order-specific value, e.g. the ability id of <see cref="OrderType.UseAbility"/>.</summary>
        public int Argument;

        public readonly bool Has(OrderTraits traits) => (Traits & traits) != 0;

        /// <summary>This order with its engine traits; a game type keeps the traits its issuer set.</summary>
        public readonly Order WithTypeTraits()
        {
            if (Type >= OrderType.GameFirst)
            {
                return this;
            }

            var order = this;
            order.Traits = EngineTraits(Type);
            return order;
        }

        private static OrderTraits EngineTraits(OrderType type) => type switch
        {
            OrderType.Move => OrderTraits.Formation,
            OrderType.AttackMove or OrderType.Patrol =>
                OrderTraits.Formation | OrderTraits.AttackMove | OrderTraits.EngagesWhileMoving,
            OrderType.Escort => OrderTraits.EngagesWhileMoving,
            _ => OrderTraits.None,
        };
    }
}
