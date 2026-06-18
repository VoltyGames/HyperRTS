using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Attack
{
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

    /// <summary>
    /// Gates how often an attacker deals damage. <see cref="AttackSystem"/> ticks
    /// <see cref="TimeRemaining"/> down and applies damage when it reaches zero.
    /// </summary>
    public struct AttackCooldown : IComponentData
    {
        /// <summary>Seconds between attacks.</summary>
        public float Interval;

        /// <summary>Seconds remaining until the next attack is ready.</summary>
        public float TimeRemaining;
    }
}
