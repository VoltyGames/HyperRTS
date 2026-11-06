using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Combat helpers on top of <see cref="TestWorld"/>.</summary>
    public static class CombatTestKit
    {
        public static Entity AddWeapon(this TestWorld world, Entity entity, Stance stance = Stance.Aggressive,
            float range = 4f, float damage = 25f, float cooldown = 0.5f, Entity projectile = default,
            UnityObjectRef<DamageType> damageType = default, WeaponTargets targets = WeaponTargets.Surface,
            Trajectory trajectory = Trajectory.Direct, float arcHeight = 0f, float minRange = 0f, bool turreted = false,
            float traverseSpeed = 360f)
        {
            var weapon = new Weapon
            {
                Range = range,
                Damage = new DamageProfile { Amount = damage, Type = damageType },
                Cooldown = cooldown,
                ProjectilePrefab = projectile,
                ProjectileSpeed = 10f,
                Targets = targets,
                MinRange = minRange,
                Aim = new WeaponAim
                {
                    ProjectileHeight = 1f,
                    Trajectory = trajectory,
                    ArcHeight = arcHeight,
                    Turreted = turreted,
                },
            };
            Aim(ref weapon, traverseSpeed);
            var writer = world.Writer(entity);
            WeaponSetup.Add(writer, new[] { WeaponMount.Create(weapon, default) },
                CombatStance.Create(stance, world.Get<LocalTransform>(entity).Position));
            return entity;
        }

        /// <summary>Gives the entity one extra weapon beside its primary.</summary>
        public static Entity AddMount(this TestWorld world, Entity entity, float range = 4f, float damage = 10f,
            float cooldown = 0.5f, WeaponTargets targets = WeaponTargets.Surface, bool independent = false,
            float minEffect = 0f, UnityObjectRef<DamageType> damageType = default, int rounds = 0,
            float reloadTime = 0.5f, int requiredResearchTypeId = 0, AmmoRefill refill = AmmoRefill.Docked)
        {
            var weapon = new Weapon
            {
                Range = range,
                Damage = new DamageProfile { Amount = damage, Type = damageType },
                Cooldown = cooldown,
                Targets = targets,
                MinEffect = minEffect,
            };
            Aim(ref weapon, 360f);
            var ammo = Ammo.Create(rounds, reloadTime, refill);
            world.Buffer<WeaponMount>(entity).Add(WeaponMount.CreateExtra(weapon, ammo, requiredResearchTypeId, independent));
            world.Buffer<WeaponPose>(entity).Add(default);
            return entity;
        }

        /// <summary>The primary weapon's live stats.</summary>
        public static Weapon Primary(this TestWorld world, Entity entity) => world.Buffer<WeaponMount>(entity)[0].Weapon;

        /// <summary>Replaces the primary's stats, and its stat bases with them.</summary>
        public static void SetPrimary(this TestWorld world, Entity entity, in Weapon weapon)
        {
            var mounts = world.Buffer<WeaponMount>(entity);
            mounts[0] = WeaponMount.Create(weapon, mounts[0].Ammo);
        }

        /// <summary>Limits the primary to <paramref name="rounds"/> shots, starting full.</summary>
        public static void SetAmmo(this TestWorld world, Entity entity, int rounds, float reloadTime, AmmoRefill refill)
        {
            var mounts = world.Buffer<WeaponMount>(entity);
            var primary = mounts[0];
            primary.Ammo = Ammo.Create(rounds, reloadTime, refill);
            mounts[0] = primary;
        }

        public static Ammo PrimaryAmmo(this TestWorld world, Entity entity) => world.Buffer<WeaponMount>(entity)[0].Ammo;

        /// <summary>Makes the primary fire salvos of <paramref name="size"/> shots, <paramref name="interval"/> seconds apart.</summary>
        public static void SetSalvo(this TestWorld world, Entity entity, int size, float interval)
        {
            var weapon = world.Primary(entity);
            weapon.SalvoSize = size;
            weapon.SalvoInterval = interval;
            world.SetPrimary(entity, weapon);
        }

        /// <summary>Gives the primary a muzzle at each point, fired in turn.</summary>
        public static void SetMuzzles(this TestWorld world, Entity entity, params float3[] points)
        {
            var weapon = world.Primary(entity);
            weapon.Aim.Muzzle = points[0];
            weapon.Aim.HasMuzzle = true;
            weapon.MuzzleCount = (byte)points.Length;
            world.SetPrimary(entity, weapon);
            var muzzles = new WeaponMuzzle[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                muzzles[i] = new WeaponMuzzle { Point = points[i] };
            }

            var writer = world.Writer(entity);
            WeaponSetup.AddMuzzles(writer, muzzles);
        }

        /// <summary>Makes the unit deploy to fire; with <paramref name="needsDeploy"/> its primary waits for it.</summary>
        public static void AddDeployable(this TestWorld world, Entity unit, float deployTime, float packUpTime,
            bool needsDeploy = true)
        {
            var writer = world.Writer(unit);
            DeployableAuthoring.Add(writer, deployTime, packUpTime);
            var weapon = world.Primary(unit);
            weapon.NeedsDeploy = needsDeploy;
            world.SetPrimary(unit, weapon);
        }

        /// <summary>Quick turning, wide limits and a tight tolerance, so poses settle within a test's first second.</summary>
        private static void Aim(ref Weapon weapon, float traverseSpeed)
        {
            weapon.Aim.TraverseRate = math.radians(traverseSpeed);
            weapon.Aim.ElevationRate = math.radians(360f);
            weapon.Aim.MinPitch = math.radians(-30f);
            weapon.Aim.MaxPitch = math.radians(80f);
            weapon.Aim.AimTolerance = math.radians(1f);
        }

        /// <summary>An empty prefab for weapons that launch projectiles.</summary>
        public static Entity ProjectilePrefab(this TestWorld world)
        {
            var prefab = world.EntityManager.CreateEntity();
            world.Add(prefab, LocalTransform.Identity);
            return world.MakePrefab(prefab);
        }

        /// <summary>The first extra weapon.</summary>
        public static WeaponMount Mount(this TestWorld world, Entity entity) =>
            world.Buffer<WeaponMount>(entity)[1];

        public static float GetHealth(this TestWorld world, Entity entity) => world.Get<Health>(entity).Current;

        /// <summary>Drops health to zero and keeps its max, so the death systems take it from there.</summary>
        public static void Kill(this TestWorld world, Entity entity)
        {
            var health = world.Get<Health>(entity);
            health.Current = 0f;
            world.Set(entity, health);
        }

        /// <summary>No order, attack target or move destination left.</summary>
        public static bool IsIdle(this TestWorld world, Entity unit)
        {
            if (world.IsEnabled<ActiveOrder>(unit))
            {
                return false;
            }

            return !world.IsEnabled<AttackTarget>(unit) && !world.IsEnabled<MoveDestination>(unit);
        }

        public static void QueueDamage(this TestWorld world, DamageEvent hit)
        {
            using var query = world.Query(typeof(DamageQueue));
            world.EntityManager.GetBuffer<DamageEvent>(query.GetSingletonEntity()).Add(hit);
        }
    }
}
