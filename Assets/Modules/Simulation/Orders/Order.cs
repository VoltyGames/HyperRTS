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
        Custom = 128,
    }

    /// <summary>One unit instruction: where to go and/or which entity to act on.</summary>
    public struct Order
    {
        public OrderType Type;
        public float3 Position;
        public Entity Target;
    }
}
