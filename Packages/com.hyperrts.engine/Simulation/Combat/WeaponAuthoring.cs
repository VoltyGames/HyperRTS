using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Lets a unit or building attack. Without a projectile prefab hits land instantly (melee, hitscan).</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Weapon")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class WeaponAuthoring : AuthoringBehaviour
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

        [Tooltip("Radius around the impact that also takes damage; 0 hits only the target.")]
        [Min(0f)]
        public float splashRadius;

        [Tooltip("Fraction of the damage dealt at the splash edge (1 = no falloff).")]
        [Range(0f, 1f)]
        public float splashEdgeDamage = 0.5f;

        [Tooltip("Splash also hurts the shooter's own and allied units.")]
        public bool friendlyFire;

        [Tooltip("Optional damage category that armor can scale.")]
        public DamageType damageType;

        [Tooltip("Optional projectile prefab; leave empty for instant hits.")]
        public GameObject projectilePrefab;

        [Tooltip("Projectile speed in world units per second.")]
        [Min(0.1f)]
        public float projectileSpeed = 25f;

        [Tooltip("Height above the shooter's and target's pivots that projectiles fly at.")]
        [Min(0f)]
        public float projectileHeight = 1f;

        [Tooltip("What the weapon can hit: Surface (ground and naval units, buildings), Air (aircraft), or both.")]
        public WeaponTargets targets;

        [Tooltip("Distance at which idle units spot enemies; 0 uses the vision range.")]
        [Min(0f)]
        public float acquireRange;

        [Tooltip("How the unit reacts to enemies when not ordered.")]
        public Stance stance = Stance.Aggressive;

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
                    SplashRadius = authoring.splashRadius,
                    SplashEdgeFactor = authoring.splashEdgeDamage,
                    FriendlyFire = authoring.friendlyFire,
                    ProjectilePrefab = authoring.projectilePrefab != null
                        ? GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    ProjectileSpeed = authoring.projectileSpeed,
                    ProjectileHeight = authoring.projectileHeight,
                    AcquireRange = authoring.acquireRange,
                    Targets = authoring.targets,
                };

                var sink = new BakerSink(this, entity);
                WeaponSetup.Add(ref sink, weapon, authoring.stance, authoring.transform.position);
            }
        }
    }
}
