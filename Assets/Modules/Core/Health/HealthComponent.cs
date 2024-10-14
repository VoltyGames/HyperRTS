using Unity.Entities;

namespace HyperRTS.Core.Health
{
    /// <summary>
    /// Health component for entities that have health.
    /// </summary>
    public struct HealthComponent : IComponentData
    {
        public int CurrentHealth;
        public int MaxHealth;
    }
}
