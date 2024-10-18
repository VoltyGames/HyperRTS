using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Health
{
    public class HealthComponentAuthoring : MonoBehaviour
    {
        public int currentHealth;
        public int maxHealth;

        public class Baker : Baker<HealthComponentAuthoring>
        {
            public override void Bake(HealthComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new HealthComponent { CurrentHealth = authoring.currentHealth, MaxHealth = authoring.maxHealth });
            }
        }
    }
    
    /// <summary>
    /// Health component for entities that have health.
    /// </summary>
    public struct HealthComponent : IComponentData
    {
        public int CurrentHealth;
        public int MaxHealth;
    }
}
