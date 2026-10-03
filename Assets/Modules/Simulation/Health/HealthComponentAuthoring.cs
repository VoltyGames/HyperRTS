using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Health
{
    [AddComponentMenu(HyperRTSMenu.Health + "Health")]
    [Icon(HyperRTSIcons.Health)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class HealthComponentAuthoring : MonoBehaviour
    {
        [Tooltip("Starting health.")]
        public int currentHealth = 100;

        [Tooltip("Maximum health capacity.")]
        public int maxHealth = 100;

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

    public struct HealthComponent : IComponentData
    {
        public int CurrentHealth;
        public int MaxHealth;
    }
}
