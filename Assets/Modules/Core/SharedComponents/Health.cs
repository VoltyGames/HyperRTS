using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.SharedComponents
{
    /// <summary>
    /// Health component for entities that have health.
    /// </summary>
    public struct Health : IComponentData
    {
        public int CurrentHealth;
        public int MaxHealth;
    }

    public class HealthAuthoring : MonoBehaviour
    {
        public int CurrentHealth;
        public int MaxHealth;

        public class Baker : Baker<HealthAuthoring>
        {
            public override void Bake(HealthAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new Health { CurrentHealth = authoring.CurrentHealth, MaxHealth = authoring.MaxHealth });
            }
        }
    }
}
