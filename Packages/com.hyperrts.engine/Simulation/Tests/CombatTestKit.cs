using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests;

/// <summary>Combat helpers on top of <see cref="TestWorld"/>.</summary>
public static class CombatTestKit
{
    extension(TestWorld world)
    {
        public Entity AddWeapon(Entity entity) =>
            world.AddWeapon(entity, new TestWeapon());
    }

    extension(TestWorld world)
    {
        public Entity AddWeapon(Entity entity, TestWeapon settings)
        {
            var weapon = new Weapon
            {
                Range = settings.Range,
                Damage = new DamageProfile { Amount = settings.Damage, Type = settings.DamageType },
                Cooldown = settings.Cooldown,
                ProjectilePrefab = settings.Projectile,
                ProjectileSpeed = 10f,
                Targets = settings.Targets,
                MinRange = settings.MinRange,
                Aim = new WeaponAim
                {
                    ProjectileHeight = 1f,
                    Course = ShotCourse.From(settings.Trajectory),
                    ArcHeight = settings.ArcHeight,
                    Turreted = settings.Turreted,
                },
            };
            Aim(ref weapon, settings.TraverseSpeed);
            var writer = world.Writer(entity);
            WeaponSetup.Add(writer, new[] { WeaponMount.Create(weapon, default) },
                CombatStance.Create(settings.Stance, world.Get<LocalTransform>(entity).Position));
            return entity;
        }
    }

    extension(TestWorld world)
    {
        public Entity AddMount(Entity entity) =>
            world.AddMount(entity, new TestMount());
    }

    extension(TestWorld world)
    {
        /// <summary>Gives the entity one extra weapon beside its primary.</summary>
        public Entity AddMount(Entity entity, TestMount settings)
        {
            var weapon = new Weapon
            {
                Range = settings.Range,
                Damage = new DamageProfile { Amount = settings.Damage, Type = settings.DamageType },
                Cooldown = settings.Cooldown,
                Targets = settings.Targets,
                MinEffect = settings.MinEffect,
                Aim = new WeaponAim { Course = ShotCourse.Direct },
            };
            Aim(ref weapon, 360f);
            var ammo = Ammo.Create(settings.Rounds, settings.ReloadTime, settings.Refill);
            world.Buffer<WeaponMount>(entity).Add(
                WeaponMount.CreateExtra(weapon, ammo, settings.RequiredResearchTypeId, settings.Independent));
            world.Buffer<WeaponPose>(entity).Add(default);
            return entity;
        }
    }

    extension(TestWorld world)
    {
        /// <summary>The primary weapon's live stats.</summary>
        public Weapon Primary(Entity entity) => world.Buffer<WeaponMount>(entity)[0].Weapon;
    }

    extension(TestWorld world)
    {
        /// <summary>Replaces the primary's stats, and its stat bases with them.</summary>
        public void SetPrimary(Entity entity, in Weapon weapon)
        {
            var mounts = world.Buffer<WeaponMount>(entity);
            mounts[0] = WeaponMount.Create(weapon, mounts[0].Ammo);
        }
    }

    extension(TestWorld world)
    {
        /// <summary>Limits the primary to <paramref name="rounds"/> shots, starting full.</summary>
        public void SetAmmo(Entity entity, int rounds, float reloadTime, AmmoRefill refill)
        {
            var mounts = world.Buffer<WeaponMount>(entity);
            var primary = mounts[0];
            primary.Ammo = Ammo.Create(rounds, reloadTime, refill);
            mounts[0] = primary;
        }
    }

    extension(TestWorld world)
    {
        public Ammo PrimaryAmmo(Entity entity) => world.Buffer<WeaponMount>(entity)[0].Ammo;
    }

    extension(TestWorld world)
    {
        /// <summary>Makes the primary fire salvos of <paramref name="size"/> shots, <paramref name="interval"/> seconds apart.</summary>
        public void SetSalvo(Entity entity, int size, float interval)
        {
            var weapon = world.Primary(entity);
            weapon.SalvoSize = size;
            weapon.SalvoInterval = interval;
            world.SetPrimary(entity, weapon);
        }
    }

    extension(TestWorld world)
    {
        /// <summary>Makes the primary fire only on order, which gives the unit the point aim a fire mission uses.</summary>
        public void SetFiresOnOrder(Entity entity)
        {
            var weapon = world.Primary(entity);
            weapon.Role = WeaponRole.OnOrder;
            world.SetPrimary(entity, weapon);
            WeaponSetup.AddPointAim(world.Writer(entity));
        }
    }

    extension(TestWorld world)
    {
        /// <summary>Gives the primary a muzzle at each point, fired in turn.</summary>
        public void SetMuzzles(Entity entity, params float3[] points)
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
    }

    extension(TestWorld world)
    {
        /// <summary>Makes the unit deploy to fire; with <paramref name="needsDeploy"/> its primary waits for it.</summary>
        public void AddDeployable(Entity unit, float deployTime, float packUpTime,
            bool needsDeploy = true)
        {
            var writer = world.Writer(unit);
            DeployableAuthoring.Add(writer, deployTime, packUpTime);
            var weapon = world.Primary(unit);
            weapon.NeedsDeploy = needsDeploy;
            world.SetPrimary(unit, weapon);
        }
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

    extension(TestWorld world)
    {
        /// <summary>An empty prefab for weapons that launch projectiles.</summary>
        public Entity ProjectilePrefab()
        {
            var prefab = world.EntityManager.CreateEntity();
            world.Add(prefab, LocalTransform.Identity);
            return world.MakePrefab(prefab);
        }
    }

    extension(TestWorld world)
    {
        /// <summary>The first extra weapon.</summary>
        public WeaponMount Mount(Entity entity) =>
            world.Buffer<WeaponMount>(entity)[1];
    }

    extension(TestWorld world)
    {
        public float GetHealth(Entity entity) => world.Get<Health>(entity).Current;
    }

    extension(TestWorld world)
    {
        /// <summary>Drops health to zero and keeps its max, so the death systems take it from there.</summary>
        public void Kill(Entity entity)
        {
            var health = world.Get<Health>(entity);
            health.Current = 0f;
            world.Set(entity, health);
        }
    }

    extension(TestWorld world)
    {
        /// <summary>No order, attack target or move destination left.</summary>
        public bool IsIdle(Entity unit)
        {
            if (world.IsEnabled<ActiveOrder>(unit))
            {
                return false;
            }

            return !world.IsEnabled<AttackTarget>(unit) && !world.IsEnabled<MoveDestination>(unit);
        }
    }

    extension(TestWorld world)
    {
        public void QueueDamage(DamageEvent hit)
        {
            using var query = world.Query(typeof(DamageQueue));
            world.EntityManager.GetBuffer<DamageEvent>(query.GetSingletonEntity()).Add(hit);
        }
    }
}
