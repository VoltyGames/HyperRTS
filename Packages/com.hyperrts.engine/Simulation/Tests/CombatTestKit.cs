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
                DamageType = damageType,
                Targets = targets,
            };
            var writer = new EntityManagerWriter(world.EntityManager, entity);
            WeaponSetup.Add(ref writer, weapon, stance, world.Get<LocalTransform>(entity).Position);
            return entity;
        }

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
