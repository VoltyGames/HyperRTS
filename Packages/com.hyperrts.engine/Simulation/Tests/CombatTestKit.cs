using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Combat helpers on top of <see cref="TestWorld"/>.</summary>
    public static class CombatTestKit
    {
        public static Entity Arm(this TestWorld world, Entity entity, Stance stance = Stance.Aggressive,
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
                DamageType = damageType,
                Targets = targets,
            };
            var sink = new EntityManagerSink(world.EntityManager, entity);
            WeaponSetup.Add(ref sink, weapon, stance, world.Get<LocalTransform>(entity).Position);
            return entity;
        }

        public static float HealthOf(this TestWorld world, Entity entity) => world.Get<Health>(entity).Current;
    }
}
