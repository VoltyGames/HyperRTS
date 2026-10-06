using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Built-in order kinds. Games add their own from <see cref="Custom"/> upward.</summary>
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
        Custom = 128,
    }

    /// <summary>One unit instruction: where to go and/or which entity to act on.</summary>
    public struct Order
    {
        public OrderType Type;
        public float3 Position;
        public Entity Target;

        /// <summary>Order-specific value, e.g. the ability id of <see cref="OrderType.UseAbility"/>.</summary>
        public int Argument;
    }
}
