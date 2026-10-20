using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Upgrades;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Lets a unit or building attack. Without a projectile prefab hits land instantly (melee, hitscan). The flat
    /// fields are the primary weapon, which sets how the unit engages; extra weapons fire beside it.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Weapon")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class WeaponAuthoring : AuthoringBehaviour
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

        [Header("Targeting")]
        [Tooltip("Firing range in world units, measured edge to edge.")]
        [Min(0.1f)]
        public float range = 6f;

        [Tooltip("What the weapon can hit: Surface (ground and naval units, buildings), Air (aircraft), or both.")]
        public WeaponTargets targets;

        [Tooltip("Never auto-acquire targets whose armor scales this damage below this factor; 0 picks any.")]
        [Min(0f)]
        public float minEffect;

        [Tooltip("Distance at which idle units spot enemies; 0 uses the vision range.")]
        [Min(0f)]
        public float acquireRange;

        [Tooltip("Auto-acquired targets are dropped beyond this multiple of the acquire range.")]
        [Min(1f)]
        public float leashFactor = 1.5f;

        [Tooltip("How the unit reacts to enemies when not ordered.")]
        public Stance stance = Stance.Aggressive;

        [Header("Extra weapons")]
        [Tooltip("Weapons mounted beside the primary; each plays the Fire2, Fire3 or Fire4 cue.")]
        public List<WeaponSlot> extraWeapons = new();

        /// <summary>An extra weapon: the primary's fields plus its own targeting, ammo and unlock.</summary>
        [Serializable]
        public class WeaponSlot
        {
            [Tooltip("Damage per hit before armor.")]
            [Min(0f)]
            public float damage = 10f;

            [Tooltip("Seconds between shots.")]
            [Min(0.05f)]
            public float cooldown = 1f;

            [Tooltip("Optional damage category that armor can scale.")]
            public DamageType damageType;

            [Tooltip("Radius around the impact that also takes damage; 0 hits only the target.")]
            [Min(0f)]
            public float splashRadius;

            [Tooltip("Fraction of the damage dealt at the splash edge (1 = no falloff).")]
            [Range(0f, 1f)]
            public float splashEdgeDamage = 0.5f;

            [Tooltip("Splash also hurts the shooter's own and allied units.")]
            public bool friendlyFire;

            [Tooltip("Optional projectile prefab; leave empty for instant hits.")]
            public GameObject projectilePrefab;

            [Tooltip("Projectile speed in world units per second.")]
            [Min(0.1f)]
            public float projectileSpeed = 25f;

            [Tooltip("Height above the shooter's and target's pivots that projectiles fly at.")]
            [Min(0f)]
            public float projectileHeight = 1f;

            [Tooltip("Child transform shots spawn at; none spawns projectiles above the pivot.")]
            public Transform muzzle;

            [Tooltip("Firing range in world units, measured edge to edge.")]
            [Min(0.1f)]
            public float range = 6f;

            [Tooltip("What it can hit. Unless independent, only what the primary can hit too.")]
            public WeaponTargets targets;

            [Tooltip("Skip targets whose armor scales this damage below this factor; 0 shoots any.")]
            [Min(0f)]
            public float minEffect;

            [Tooltip("Picks its own target in range when it can't hit the unit's.")]
            public bool independent;

            [Tooltip("Research the owner must finish before it fires; none fires from the start.")]
            public UpgradeAuthoring requires;

            [Tooltip("Shots before it must rearm while docked; 0 never runs dry.")]
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
                    LeashFactor = authoring.leashFactor,
                    Targets = authoring.targets,
                    MinEffect = authoring.minEffect,
                };
                SetMuzzle(ref weapon, authoring.transform, authoring.muzzle);

                var writer = new BakerWriter(this, entity);
                WeaponSetup.Add(ref writer, weapon, authoring.stance, authoring.transform.position);
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
                    mounts.Add(WeaponMount.Create(BakeWeapon(authoring.transform, slot), slot.rounds, slot.reloadTime,
                        required, (byte)(i + 1), slot.independent));
                }

                return mounts;
            }

            private Weapon BakeWeapon(Transform root, WeaponSlot slot)
            {
                var weapon = new Weapon
                {
                    Range = slot.range,
                    Damage = slot.damage,
                    Cooldown = slot.cooldown,
                    DamageType = slot.damageType,
                    SplashRadius = slot.splashRadius,
                    SplashEdgeFactor = slot.splashEdgeDamage,
                    FriendlyFire = slot.friendlyFire,
                    ProjectilePrefab = slot.projectilePrefab != null
                        ? GetEntity(slot.projectilePrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    ProjectileSpeed = slot.projectileSpeed,
                    ProjectileHeight = slot.projectileHeight,
                    Targets = slot.targets,
                    MinEffect = slot.minEffect,
                };
                SetMuzzle(ref weapon, root, slot.muzzle);
                return weapon;
            }

            private void SetMuzzle(ref Weapon weapon, Transform root, Transform muzzle)
            {
                if (muzzle == null)
                {
                    return;
                }

                DependsOn(muzzle);
                weapon.Muzzle = EffectSetup.Socket(root, muzzle);
                weapon.HasMuzzle = true;
            }
        }
    }
}
