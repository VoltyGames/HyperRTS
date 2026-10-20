using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Combat helpers on top of <see cref="TestWorld"/>.</summary>
    public static class CombatTestKit
    {
        public static Entity AddWeapon(this TestWorld world, Entity entity, Stance stance = Stance.Aggressive,
            float range = 4f, float damage = 25f, float cooldown = 0.5f, Entity projectile = default,
            UnityObjectRef<DamageType> damageType = default, WeaponTargets targets = WeaponTargets.Surface)
        {
            var weapon = new Weapon
            {
                Range = range,
                Damage = damage,
                Cooldown = cooldown,
                ProjectilePrefab = projectile,
                ProjectileSpeed = 10f,
                ProjectileHeight = 1f,
                LeashFactor = 1.5f,
                DamageType = damageType,
                Targets = targets,
            };
            var writer = new EntityManagerWriter(world.EntityManager, entity);
            WeaponSetup.Add(ref writer, weapon, stance, world.Get<LocalTransform>(entity).Position);
            return entity;
        }

        /// <summary>Gives the entity one extra weapon beside its primary.</summary>
        public static Entity AddMount(this TestWorld world, Entity entity, float range = 4f, float damage = 10f,
            float cooldown = 0.5f, WeaponTargets targets = WeaponTargets.Surface, bool independent = false,
            float minEffect = 0f, UnityObjectRef<DamageType> damageType = default, int rounds = 0,
            float reloadTime = 0.5f, int requiredResearchTypeId = 0)
        {
            var weapon = new Weapon
            {
                Range = range,
                Damage = damage,
                Cooldown = cooldown,
                DamageType = damageType,
                Targets = targets,
                MinEffect = minEffect,
            };
            var mount = WeaponMount.Create(weapon, rounds, reloadTime, requiredResearchTypeId, 1, independent);
            var writer = new EntityManagerWriter(world.EntityManager, entity);
            WeaponSetup.AddMounts(ref writer, new[] { mount });
            return entity;
        }

        public static WeaponMount Mount(this TestWorld world, Entity entity) =>
            world.EntityManager.GetBuffer<WeaponMount>(entity)[0];

        public static float GetHealth(this TestWorld world, Entity entity) => world.Get<Health>(entity).Current;

        /// <summary>Drops health to zero and keeps its max, so the death systems take it from there.</summary>
        public static void Kill(this TestWorld world, Entity entity)
        {
            var health = world.Get<Health>(entity);
            health.Current = 0f;
            world.EntityManager.SetComponentData(entity, health);
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
            using var query = world.EntityManager.CreateEntityQuery(typeof(DamageQueue));
            world.EntityManager.GetBuffer<DamageEvent>(query.GetSingletonEntity()).Add(hit);
        }
    }
}
