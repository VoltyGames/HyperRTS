using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Lets a unit or building attack. Without a projectile prefab hits land instantly (melee, hitscan).</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Weapon")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class WeaponAuthoring : MonoBehaviour
    {
        [Tooltip("Firing range in world units, measured edge to edge.")]
        [Min(0.1f)]
        public float range = 6f;

        [Tooltip("Damage per hit before armor.")]
        [Min(0f)]
        public float damage = 10f;

        [Tooltip("Seconds between shots.")]
        [Min(0.05f)]
        public float cooldown = 1f;

        [Tooltip("Optional damage category that armor can scale.")]
        public DamageType damageType;

        [Tooltip("Optional projectile prefab; leave empty for instant hits.")]
        public GameObject projectilePrefab;

        [Tooltip("Projectile speed in world units per second.")]
        [Min(0.1f)]
        public float projectileSpeed = 25f;

        [Tooltip("Distance at which idle units spot enemies; 0 uses the vision range.")]
        [Min(0f)]
        public float acquireRange;

        [Tooltip("How the unit reacts to enemies when not ordered.")]
        public Stance stance = Stance.Aggressive;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, range);
        }

        public class Baker : Baker<WeaponAuthoring>
        {
            public override void Bake(WeaponAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var weapon = new Weapon
                {
                    Range = authoring.range,
                    Damage = authoring.damage,
                    Cooldown = authoring.cooldown,
                    DamageType = authoring.damageType,
                    ProjectilePrefab = authoring.projectilePrefab != null
                        ? GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    ProjectileSpeed = authoring.projectileSpeed,
                    AcquireRange = authoring.acquireRange,
                };

                var sink = new BakerSink(this, entity);
                WeaponSetup.Add(ref sink, weapon, authoring.stance, authoring.transform.position);
            }
        }
    }

    /// <summary>Attack stats; range is measured edge to edge between footprints.</summary>
    public struct Weapon : IComponentData
    {
        public float Range;
        public float Damage;
        public float Cooldown;
        public float CooldownRemaining;
        public UnityObjectRef<DamageType> DamageType;
        public Entity ProjectilePrefab;
        public float ProjectileSpeed;
        public float AcquireRange;
    }
}
