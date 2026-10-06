using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Hit points; the entity dies when they reach zero.</summary>
    public struct Health : IComponentData
    {
        [GhostField] public float Current;
        [GhostField] public float Max;

        public readonly float Fraction => Max > 0f ? math.saturate(Current / Max) : 0f;

        /// <summary>False for entities without health, so dead and missing targets are treated alike.</summary>
        public static bool IsAlive(in ComponentLookup<Health> lookup, Entity entity) =>
            lookup.TryGetComponent(entity, out var health) && health.Current > 0f;
    }
}
