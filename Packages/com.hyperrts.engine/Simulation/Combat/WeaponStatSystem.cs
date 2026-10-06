using HyperRTS.Core;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Rewrites weapon damage, range and cooldown from their bases and the stat modifiers on change.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct WeaponStatSystem : ISystem
    {
        /// <summary>Floor on the fire-rate multiplier, keeping the cooldown finite under heavy slows.</summary>
        public const float MinFireRate = 0.01f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new ApplyJob().ScheduleParallel();

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        private partial struct ApplyJob : IJobEntity
        {
            private void Execute(ref Weapon weapon, in DynamicBuffer<StatModifier> modifiers,
                DynamicBuffer<BaseStat> bases)
            {
                weapon.Damage = StatMath.Apply(modifiers, bases, Stat.Damage, weapon.Damage);
                weapon.Range = StatMath.Apply(modifiers, bases, Stat.Range, weapon.Range);
                var fireRate = math.max(MinFireRate, StatMath.Evaluate(modifiers, Stat.FireRate, 1f));
                weapon.Cooldown = StatMath.BaseOf(bases, Stat.FireRate, weapon.Cooldown) / fireRate;
            }
        }
    }
}
