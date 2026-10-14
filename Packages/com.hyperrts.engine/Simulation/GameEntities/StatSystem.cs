using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>
    /// Rewrites every live stat from its recorded base and the <see cref="StatModifier"/>s whenever they change;
    /// health keeps its fraction when the cap moves.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct StatSystem : ISystem
    {
        /// <summary>Floor on the fire-rate multiplier, keeping the cooldown finite under heavy slows.</summary>
        public const float MinFireRate = 0.01f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MaxHealthJob().ScheduleParallel();
            new WeaponJob().ScheduleParallel();
            new MoveSpeedJob().ScheduleParallel();
            new VisionJob().ScheduleParallel();
            new BuildRateJob().ScheduleParallel();
            new ProductionSpeedJob().ScheduleParallel();
        }

        /// <summary>
        /// The cooldown for a fire rate of <c>(1 / baseCooldown + Add) × (1 + ΣPercent)</c> shots per second, worked in
        /// cooldown space so an unmodified weapon keeps its exact authored cooldown.
        /// </summary>
        public static float Cooldown(DynamicBuffer<StatModifier> modifiers, float baseCooldown)
        {
            StatMath.SumModifiers(modifiers, Stat.FireRate, out var add, out var percent);
            var multiplier = math.max(MinFireRate, (1f + add * baseCooldown) * (1f + percent));
            return baseCooldown / multiplier;
        }

        // Entities spawned this frame wait for StatBaseSystem, which marks their modifiers changed once bases exist.
        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        [WithDisabled(typeof(StatBasesPending))]
        private partial struct MaxHealthJob : IJobEntity
        {
            private void Execute(ref Health health, in DynamicBuffer<StatModifier> modifiers,
                in DynamicBuffer<BaseStat> bases)
            {
                var max = StatMath.Apply(modifiers, bases, Stat.MaxHealth);
                health.Current = health.Max > 0f ? health.Current * max / health.Max : max;
                health.Max = max;
            }
        }

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        [WithDisabled(typeof(StatBasesPending))]
        private partial struct WeaponJob : IJobEntity
        {
            private void Execute(ref Weapon weapon, in DynamicBuffer<StatModifier> modifiers,
                in DynamicBuffer<BaseStat> bases)
            {
                weapon.Damage = StatMath.Apply(modifiers, bases, Stat.Damage);
                weapon.Range = StatMath.Apply(modifiers, bases, Stat.Range);
                weapon.Cooldown = Cooldown(modifiers, StatMath.BaseValue(bases, Stat.FireRate));
            }
        }

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        [WithDisabled(typeof(StatBasesPending))]
        private partial struct MoveSpeedJob : IJobEntity
        {
            private void Execute(ref MovementSpeed speed, in DynamicBuffer<StatModifier> modifiers,
                in DynamicBuffer<BaseStat> bases)
            {
                speed.Value = StatMath.Apply(modifiers, bases, Stat.MoveSpeed);
            }
        }

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        [WithDisabled(typeof(StatBasesPending))]
        private partial struct VisionJob : IJobEntity
        {
            private void Execute(ref VisionRange vision, in DynamicBuffer<StatModifier> modifiers,
                in DynamicBuffer<BaseStat> bases)
            {
                vision.Value = StatMath.Apply(modifiers, bases, Stat.VisionRange);
            }
        }

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        [WithDisabled(typeof(StatBasesPending))]
        private partial struct BuildRateJob : IJobEntity
        {
            private void Execute(ref Builder builder, in DynamicBuffer<StatModifier> modifiers,
                in DynamicBuffer<BaseStat> bases)
            {
                builder.Rate = StatMath.Apply(modifiers, bases, Stat.BuildRate);
            }
        }

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        [WithDisabled(typeof(StatBasesPending))]
        private partial struct ProductionSpeedJob : IJobEntity
        {
            private void Execute(ref Producer producer, in DynamicBuffer<StatModifier> modifiers,
                in DynamicBuffer<BaseStat> bases)
            {
                producer.Speed = StatMath.Apply(modifiers, bases, Stat.ProductionSpeed);
            }
        }
    }
}
