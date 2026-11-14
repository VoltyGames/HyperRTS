using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>What one weapon deals and how it reaches; the primary and every extra weapon carry one.</summary>
    [Serializable]
    public class WeaponStats
    {
        [Tooltip("The weapon's designation, shown on a squad member's card (M4A1, PKP). Empty for none.")]
        public string designation;

        [Header("Damage")]
        [Tooltip("What each hit deals, its damage type and its splash.")]
        public DamageSettings hit = new();

        [Tooltip("Seconds between shots.")]
        [Min(0.05f)]
        public float cooldown = 1f;

        [Header("Projectile")]
        [Tooltip("Optional projectile prefab; leave empty for instant hits.")]
        [PrefabAsset("Projectile"), GhostPrefab("Projectile")]
        public GameObject projectilePrefab;

        [Tooltip("Child transform shots spawn at; none spawns projectiles above the pivot.")]
        public Transform muzzle;

        [Tooltip("More muzzles after the first, such as a launcher's other tubes; shots leave from each in turn.")]
        public List<Transform> extraMuzzles = new();

        [Tooltip("Projectile speed.")]
        [Unit("m/s")]
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

        [Tooltip("How fast the weapon traverses; a turreted weapon fires once it has turned onto the target.")]
        [Unit("°/s")]
        [Min(1f)]
        public float traverseSpeed = 40f;

        [Tooltip("How fast the weapon elevates.")]
        [Unit("°/s")]
        [Min(1f)]
        public float elevationSpeed = 25f;

        [Tooltip("Lowest elevation in degrees (negative is depression).")]
        [Range(-90f, 0f)]
        public float minElevation = -10f;

        [Tooltip("Highest elevation in degrees.")]
        [Range(0f, 90f)]
        public float maxElevation = 20f;

        [Tooltip("Elevation in degrees it raises to once its Deployable unit has deployed, while it has nothing to aim at (a launcher held up to fire). It lowers as the unit packs up, within the limits above. 0 stays level.")]
        [Range(0f, 90f)]
        public float deployedElevation;

        [Tooltip("Degrees off target, per axis, a turret may fire at. A unit's hull-fixed weapon fires once the hull is this close.")]
        [Min(0.1f)]
        public float aimTolerance = 2f;

        [Header("Targeting")]
        [Tooltip("Firing range, measured edge to edge.")]
        [Unit("m")]
        [Min(0.1f)]
        public float range = 6f;

        [Tooltip("Targets closer than this, edge to edge, can't be hit; ordered attacks back off first.")]
        [Min(0f)]
        public float minRange;

        [Tooltip("What the weapon can hit: Ground (units and buildings), Naval, Air (aircraft) and Projectile (shells and missiles in flight).")]
        public WeaponTargets targets = WeaponTargets.Surface;

        [Tooltip("Projectile kinds it shoots at when its targets include Projectile; the range is how far out it engages.")]
        public ProjectileKind interceptKinds = ProjectileKind.Shell | ProjectileKind.Rocket | ProjectileKind.Missile;

        [Tooltip("Chance that one shot at a projectile destroys it; a miss lets it fly on.")]
        [Range(0f, 1f)]
        public float killChance = 0.5f;

        [Tooltip("Never auto-acquire targets whose armor scales this damage below this factor; 0 picks any.")]
        [Min(0f)]
        public float minEffect;

        [Tooltip("Holds fire until its Deployable unit has deployed.")]
        public bool needsDeploy;

        [Tooltip("Half angle in degrees of the cone it fires into, about the way its unit faced when it deployed (or faces now if it never deploys). 0 fires all round.")]
        [Range(0f, 180f)]
        public float firingArc;

        [Header("Ammo")]
        [Tooltip("Shots before it must reload; 0 never runs dry. An empty weapon holds fire (an aircraft with a pad flies home).")]
        [Min(0)]
        public int rounds;

        [Tooltip("Seconds to reload one round, or the whole magazine with Magazine.")]
        [Min(0.05f)]
        public float reloadTime = 1f;

        [Tooltip("Docked reloads by the round only at an airfield, Round by the round anywhere, Magazine every round at once once empty.")]
        public AmmoRefill refill;

        [Header("Salvo")]
        [Tooltip("Shots fired at one target before the cooldown starts; 1 fires single shots.")]
        [Min(1)]
        public int salvoSize = 1;

        [Tooltip("Seconds between the shots of a salvo.")]
        [Min(0.05f)]
        public float salvoInterval = 0.25f;

        [Tooltip("Shots land at random within this distance of the aim point; a homing shot with a spread gives up its lock and flies to its point, so leave it 0 on missiles.")]
        [Unit("m")]
        [Min(0f)]
        public float spread;

        /// <summary>Shoots projectiles in flight, so it is an interceptor.</summary>
        public bool ShootsProjectiles => (targets & WeaponTargets.Projectile) != WeaponTargets.None;

        /// <summary>Damage per second before armor, with the cooldown floored at its minimum.</summary>
        public float Dps => hit.damage / Mathf.Max(cooldown, 0.05f);

        /// <summary>The weapon's magazine, full.</summary>
        public Ammo BakeAmmo() => Ammo.Create(rounds, reloadTime, refill);

        /// <summary>
        /// The baked weapon, with its muzzle sockets relative to <paramref name="root"/>. Several muzzles go on the end
        /// of <paramref name="muzzles"/>, the shooter's <see cref="WeaponMuzzle"/> buffer.
        /// </summary>
        public Weapon Bake(IBaker baker, Transform root, List<WeaponMuzzle> muzzles)
        {
            var weapon = new Weapon
            {
                Range = range,
                Damage = hit.Bake(),
                Cooldown = cooldown,
                ProjectilePrefab = projectilePrefab != null
                    ? baker.GetEntity(projectilePrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null,
                ProjectileSpeed = projectileSpeed,
                ProjectileKind = projectileKind,
                Targets = targets,
                InterceptKinds = interceptKinds,
                KillChance = killChance,
                MinEffect = minEffect,
                MinRange = minRange,
                SalvoSize = salvoSize,
                SalvoInterval = salvoInterval,
                Spread = spread,
                NeedsDeploy = needsDeploy,
                FiringArc = math.radians(firingArc),
                Aim = new WeaponAim
                {
                    TraverseRate = math.radians(traverseSpeed),
                    ElevationRate = math.radians(elevationSpeed),
                    MinPitch = math.radians(minElevation),
                    MaxPitch = math.radians(maxElevation),
                    DeployedPitch = math.radians(math.clamp(deployedElevation, minElevation, maxElevation)),
                    AimTolerance = math.radians(aimTolerance),
                    ProjectileHeight = projectileHeight,
                    Course = ShotCourse.From(trajectory),
                    ArcHeight = arcHeight,
                },
            };
            weapon.Aim.BakeSockets(baker, root, muzzle, turret, barrel);
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
            muzzles.Add(new WeaponMuzzle { Point = weapon.Aim.Muzzle });
            foreach (var extra in extraMuzzles)
            {
                muzzles.Add(new WeaponMuzzle { Point = AuthoringBehaviour.Socket(baker, root, extra) });
            }
        }
    }
}
