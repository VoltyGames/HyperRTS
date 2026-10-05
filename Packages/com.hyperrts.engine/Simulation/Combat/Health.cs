using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Hit points; the entity dies when they reach zero.</summary>
    public struct Health : IComponentData
    {
        public float Current;
        public float Max;

        public readonly float Fraction => Max > 0f ? math.saturate(Current / Max) : 0f;
    }
}
