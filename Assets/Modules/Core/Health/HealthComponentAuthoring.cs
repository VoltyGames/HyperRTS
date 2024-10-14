using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Health
{
    public class HealthComponentAuthoring : MonoBehaviour
    {
        public int CurrentHealth;
        public int MaxHealth;

        public class Baker : Baker<HealthComponentAuthoring>
        {
            public override void Bake(HealthComponentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new HealthComponent { CurrentHealth = authoring.CurrentHealth, MaxHealth = authoring.MaxHealth });
            }
        }
    }
}
