using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Attack
{
    [AddComponentMenu(HyperRTSMenu.Attack + "Attack Cooldown")]
    [Icon(HyperRTSIcons.Attack)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class AttackCooldownAuthoring : MonoBehaviour
    {
        [Tooltip("Seconds between attacks.")]
        public float interval = 1f;

        public class Baker : Baker<AttackCooldownAuthoring>
        {
            public override void Bake(AttackCooldownAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new AttackCooldown { Interval = authoring.interval, TimeRemaining = 0f });
            }
        }
    }

    /// <summary>Attack rate limiter, ticked by <see cref="AttackSystem"/>.</summary>
    public struct AttackCooldown : IComponentData
    {
        /// <summary>Seconds between attacks.</summary>
        public float Interval;

        public float TimeRemaining;
    }
}
