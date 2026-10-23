using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Upgrades;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Lets a unit or building attack. Without a projectile prefab hits land instantly (melee, hitscan). The stats
    /// are the primary weapon, which sets how the unit engages; extra weapons fire beside it.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Weapon")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class WeaponAuthoring : AuthoringBehaviour
    {
        [Tooltip("What the primary weapon deals and how it reaches; it sets how the unit engages.")]
        public WeaponStats stats = new();

        [Header("Engagement")]
        [Tooltip("A turret aims at the target, so the hull keeps its heading while firing. Buildings always aim a turret.")]
        public bool turreted;

        [Tooltip("Distance at which idle units spot enemies; 0 uses the vision range.")]
        [Min(0f)]
        public float acquireRange;

        [Tooltip("Auto-acquired targets are dropped beyond this multiple of the acquire range.")]
        [Min(1f)]
        public float leashFactor = 1.5f;

        [Tooltip("How the unit reacts to enemies when not ordered.")]
        public Stance stance = Stance.Aggressive;

        [Header("Extra weapons")]
        [Tooltip("Weapons mounted beside the primary; each plays its own Fire cue by weapon index.")]
        public List<WeaponSlot> extraWeapons = new();

        /// <summary>A building never turns, so its weapon always aims a turret.</summary>
        public bool AimsTurret => turreted || TryGetComponent<BuildingAuthoring>(out _);

        /// <summary>Pose buffer slots the primary and extra weapons take; an interceptor's pose follows them.</summary>
        public int WeaponPoses => 1 + extraWeapons.Count;

        /// <summary>What one weapon deals and how it reaches; the primary and every extra weapon carry one.</summary>
        [Serializable]
        public class WeaponStats
        {
            [Header("Damage")]
            [Tooltip("Damage per hit before armor.")]
            [Min(0f)]
            public float damage = 10f;

            [Tooltip("Seconds between shots.")]
            [Min(0.05f)]
            public float cooldown = 1f;

            [Tooltip("Optional damage category that armor can scale.")]
            public DamageType damageType;

            [Header("Splash")]
            [Tooltip("Radius around the impact that also takes damage; 0 hits only the target.")]
            [Min(0f)]
            public float splashRadius;

            [Tooltip("Fraction of the damage dealt at the splash edge (1 = no falloff).")]
            [Range(0f, 1f)]
            [ShowIf(nameof(splashRadius))]
            public float splashEdgeDamage = 0.5f;

            [Tooltip("Splash also hurts the shooter's own and allied units.")]
            [ShowIf(nameof(splashRadius))]
            public bool friendlyFire;

            [Header("Projectile")]
            [Tooltip("Optional projectile prefab; leave empty for instant hits.")]
            public GameObject projectilePrefab;

            [Tooltip("Child transform shots spawn at; none spawns projectiles above the pivot.")]
            public Transform muzzle;

            [Tooltip("Projectile speed in world units per second.")]
            [Min(0.1f)]
            [ShowIf(nameof(projectilePrefab))]
            public float projectileSpeed = 25f;

            [Tooltip("Height above the shooter's and target's pivots that projectiles fly at.")]
            [Min(0f)]
            [ShowIf(nameof(projectilePrefab))]
            public float projectileHeight = 1f;

            [Tooltip("Direct flies straight, Ballistic arcs onto where the target stood, Rocket climbs and dives onto it.")]
            [ShowIf(nameof(projectilePrefab))]
            public Trajectory trajectory;

            [Tooltip("Arc apex as a fraction of the ground distance to the target.")]
            [Min(0f)]
            [ShowIf(nameof(projectilePrefab))]
            public float arcHeight = 0.25f;

            [Tooltip("What interceptors see the projectile as; None is never shot down.")]
            [ShowIf(nameof(projectilePrefab))]
            public ProjectileKind projectileKind;

            [Header("Aim")]
            [Tooltip("Part the muzzle traverses about, such as the turret; none traverses about the unit's pivot.")]
            public Transform turret;

            [Tooltip("Part the muzzle elevates about, such as the barrel; none raises only the aim, not the muzzle.")]
            public Transform barrel;

            [Tooltip("Degrees per second the weapon traverses; a turreted weapon fires once it has turned onto the target.")]
            [Min(1f)]
            public float traverseSpeed = 40f;

            [Tooltip("Degrees per second the weapon elevates.")]
            [Min(1f)]
            public float elevationSpeed = 25f;

            [Tooltip("Lowest elevation in degrees (negative is depression).")]
            [Range(-90f, 0f)]
            public float minElevation = -10f;

            [Tooltip("Highest elevation in degrees.")]
            [Range(0f, 90f)]
            public float maxElevation = 20f;

            [Tooltip("Degrees off target, per axis, a turret may fire at. A unit's hull-fixed weapon fires once the hull is this close.")]
            [Min(0.1f)]
            public float aimTolerance = 2f;

            [Header("Targeting")]
            [Tooltip("Firing range in world units, measured edge to edge.")]
            [Min(0.1f)]
            public float range = 6f;

            [Tooltip("Targets closer than this, edge to edge, can't be hit; ordered attacks back off first.")]
            [Min(0f)]
            public float minRange;

            [Tooltip("What the weapon can hit: Surface (ground and naval units, buildings), Air (aircraft), or both.")]
            public WeaponTargets targets;

            [Tooltip("Never auto-acquire targets whose armor scales this damage below this factor; 0 picks any.")]
            [Min(0f)]
            public float minEffect;

            /// <summary>Damage per second before armor, with the cooldown floored at its minimum.</summary>
            public float Dps => damage / Mathf.Max(cooldown, 0.05f);

            /// <summary>The baked weapon, with its muzzle socket relative to <paramref name="root"/>.</summary>
            public Weapon Bake(IBaker baker, Transform root)
            {
                var weapon = new Weapon
                {
                    Range = range,
                    Damage = damage,
                    Cooldown = cooldown,
                    DamageType = damageType,
                    SplashRadius = splashRadius,
                    SplashEdgeFactor = splashEdgeDamage,
                    FriendlyFire = friendlyFire,
                    ProjectilePrefab = projectilePrefab != null
                        ? baker.GetEntity(projectilePrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    ProjectileSpeed = projectileSpeed,
                    ProjectileKind = projectileKind,
                    ProjectileHeight = projectileHeight,
                    Targets = targets,
                    MinEffect = minEffect,
                    Trajectory = trajectory,
                    ArcHeight = arcHeight,
                    MinRange = minRange,
                    TraverseRate = math.radians(traverseSpeed),
                    ElevationRate = math.radians(elevationSpeed),
                    MinPitch = math.radians(minElevation),
                    MaxPitch = math.radians(maxElevation),
                    AimTolerance = math.radians(aimTolerance),
                };
                if (muzzle != null)
                {
                    weapon.Muzzle = Pivot(baker, root, muzzle);
                    weapon.HasMuzzle = true;
                }

                weapon.TurretPivot = turret != null ? Pivot(baker, root, turret) : float3.zero;
                weapon.BarrelPivot = barrel != null ? Pivot(baker, root, barrel) : weapon.Muzzle;
                return weapon;
            }

            /// <summary>Where <paramref name="part"/> sits in <paramref name="root"/>'s space; interceptors aim about these too.</summary>
            internal static float3 Pivot(IBaker baker, Transform root, Transform part)
            {
                baker.DependsOn(part);
                return EffectSetup.Socket(root, part);
            }
        }

        /// <summary>An extra weapon: its own stats plus its targeting, ammo and unlock.</summary>
        [Serializable]
        public class WeaponSlot
        {
            [Tooltip("What this weapon deals and how it reaches.")]
            public WeaponStats stats = new();

            [Tooltip("Picks its own target when it can't hit the unit's; otherwise only what the primary hits.")]
            public bool independent;

            [Tooltip("Research the owner must finish before it fires; none fires from the start.")]
            public UpgradeAuthoring requires;

            [Tooltip("Shots before it must reload while docked; 0 never runs dry.")]
            [Min(0)]
            public int rounds;

            [Tooltip("Seconds per round refilled while docked.")]
            [Min(0.05f)]
            public float reloadTime = 1f;
        }

        public class Baker : Baker<WeaponAuthoring>
        {
            public override void Bake(WeaponAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var weapon = authoring.stats.Bake(this, authoring.transform);
                weapon.AcquireRange = authoring.acquireRange;
                weapon.LeashFactor = authoring.leashFactor;
                weapon.Turreted = authoring.AimsTurret;

                var writer = new BakerWriter(this, entity);
                var poses = authoring.WeaponPoses + (GetComponent<InterceptorAuthoring>() != null ? 1 : 0);
                WeaponSetup.Add(ref writer, weapon, authoring.stance, authoring.transform.position, poses - 1);
                if (authoring.extraWeapons.Count > 0)
                {
                    WeaponSetup.AddMounts(ref writer, BakeMounts(authoring));
                }
            }

            private List<WeaponMount> BakeMounts(WeaponAuthoring authoring)
            {
                var mounts = new List<WeaponMount>();
                for (var i = 0; i < authoring.extraWeapons.Count; i++)
                {
                    var slot = authoring.extraWeapons[i];
                    if (slot.requires != null)
                    {
                        DependsOn(slot.requires);
                    }

                    var required = slot.requires != null ? slot.requires.TypeId : 0;
                    var weapon = slot.stats.Bake(this, authoring.transform);
                    mounts.Add(WeaponMount.Create(weapon, slot.rounds, slot.reloadTime, required, (byte)(i + 1),
                        slot.independent));
                }

                return mounts;
            }
        }
    }
}
