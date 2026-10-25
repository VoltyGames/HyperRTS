using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
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

        [Tooltip("More muzzles after the first, such as a launcher's other tubes; shots leave from each in turn.")]
        public List<Transform> extraMuzzles = new();

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

        [Tooltip("Holds fire until its Deployable unit has deployed.")]
        public bool needsDeploy;

        [Header("Salvo")]
        [Tooltip("Shots fired at one target before the cooldown starts; 1 fires single shots.")]
        [Min(1)]
        public int salvoSize = 1;

        [Tooltip("Seconds between the shots of a salvo.")]
        [Min(0.05f)]
        public float salvoInterval = 0.25f;

        [Tooltip("Shots land at random within this many metres of the aim point; a homing shot with a spread gives up its lock and flies to its point, so leave it 0 on missiles.")]
        [Min(0f)]
        public float spread;

        /// <summary>Damage per second before armor, with the cooldown floored at its minimum.</summary>
        public float Dps => damage / Mathf.Max(cooldown, 0.05f);

        /// <summary>
        /// The baked weapon, with its muzzle sockets relative to <paramref name="root"/>. Several muzzles go on the end
        /// of <paramref name="muzzles"/>, the shooter's <see cref="WeaponMuzzle"/> buffer.
        /// </summary>
        public Weapon Bake(IBaker baker, Transform root, List<WeaponMuzzle> muzzles)
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
                SalvoSize = salvoSize,
                SalvoInterval = salvoInterval,
                Spread = spread,
                NeedsDeploy = needsDeploy,
            };
            if (muzzle != null)
            {
                weapon.Muzzle = EffectSetup.Socket(baker, root, muzzle);
                weapon.HasMuzzle = true;
            }

            weapon.TurretPivot = EffectSetup.Socket(baker, root, turret);
            weapon.BarrelPivot = barrel != null ? EffectSetup.Socket(baker, root, barrel) : weapon.Muzzle;
            BakeMuzzles(baker, root, muzzles, ref weapon);
            return weapon;
        }

        private void BakeMuzzles(IBaker baker, Transform root, List<WeaponMuzzle> muzzles, ref Weapon weapon)
        {
            if (extraMuzzles.Count == 0)
            {
                return;
            }

            if (muzzle == null)
            {
                throw new InvalidOperationException($"Weapon on '{root.name}' has extra muzzles but no first muzzle.");
            }

            weapon.FirstMuzzle = (byte)muzzles.Count;
            weapon.MuzzleCount = (byte)(1 + extraMuzzles.Count);
            muzzles.Add(new WeaponMuzzle { Point = weapon.Muzzle });
            foreach (var extra in extraMuzzles)
            {
                muzzles.Add(new WeaponMuzzle { Point = EffectSetup.Socket(baker, root, extra) });
            }
        }
    }
}
