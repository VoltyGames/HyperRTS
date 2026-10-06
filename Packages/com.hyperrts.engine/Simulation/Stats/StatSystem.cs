using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>
    /// Rewrites live stats (health cap, weapon, speed, vision, build and production rates) from <see cref="BaseStats"/>
    /// and the <see cref="StatModifier"/> buffer whenever it changes. Health keeps its fraction when the cap moves.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct StatSystem : ISystem
    {
        private ComponentLookup<Weapon> _weapons;
        private ComponentLookup<MovementSpeed> _speeds;
        private ComponentLookup<Builder> _builders;
        private ComponentLookup<Producer> _producers;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _weapons = state.GetComponentLookup<Weapon>();
            _speeds = state.GetComponentLookup<MovementSpeed>();
            _builders = state.GetComponentLookup<Builder>();
            _producers = state.GetComponentLookup<Producer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _weapons.Update(ref state);
            _speeds.Update(ref state);
            _builders.Update(ref state);
            _producers.Update(ref state);

            new ApplyJob
            {
                Weapons = _weapons,
                Speeds = _speeds,
                Builders = _builders,
                Producers = _producers,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        private partial struct ApplyJob : IJobEntity
        {
            // Each entity writes only its own components.
            [NativeDisableParallelForRestriction] public ComponentLookup<Weapon> Weapons;
            [NativeDisableParallelForRestriction] public ComponentLookup<MovementSpeed> Speeds;
            [NativeDisableParallelForRestriction] public ComponentLookup<Builder> Builders;
            [NativeDisableParallelForRestriction] public ComponentLookup<Producer> Producers;

            private void Execute(Entity entity, ref BaseStats stats, DynamicBuffer<StatModifier> modifiers,
                ref Health health, ref VisionRange vision)
            {
                if (!stats.Captured)
                {
                    stats = Capture(entity, health, vision);
                }

                var max = StatMath.Evaluate(modifiers, Stat.MaxHealth, stats.MaxHealth);
                health.Current = health.Max > 0f ? health.Current * max / health.Max : max;
                health.Max = max;
                vision.Value = StatMath.Evaluate(modifiers, Stat.VisionRange, stats.VisionRange);

                if (Weapons.HasComponent(entity))
                {
                    ref var weapon = ref Weapons.GetRefRW(entity).ValueRW;
                    weapon.Damage = StatMath.Evaluate(modifiers, Stat.Damage, stats.Damage);
                    weapon.Range = StatMath.Evaluate(modifiers, Stat.Range, stats.Range);
                    weapon.Cooldown = stats.Cooldown / math.max(0.01f, StatMath.Evaluate(modifiers, Stat.FireRate, 1f));
                }

                if (Speeds.HasComponent(entity))
                {
                    Speeds[entity] = new MovementSpeed { Value = StatMath.Evaluate(modifiers, Stat.MoveSpeed, stats.MoveSpeed) };
                }

                if (Builders.HasComponent(entity))
                {
                    Builders.GetRefRW(entity).ValueRW.Rate = StatMath.Evaluate(modifiers, Stat.BuildRate, stats.BuildRate);
                }

                if (Producers.HasComponent(entity))
                {
                    Producers.GetRefRW(entity).ValueRW.Speed =
                        StatMath.Evaluate(modifiers, Stat.ProductionSpeed, stats.ProductionSpeed);
                }
            }

            private BaseStats Capture(Entity entity, in Health health, in VisionRange vision)
            {
                var weapon = Weapons.TryGetComponent(entity, out var w) ? w : default;
                return new BaseStats
                {
                    Captured = true,
                    MaxHealth = health.Max,
                    VisionRange = vision.Value,
                    Damage = weapon.Damage,
                    Range = weapon.Range,
                    Cooldown = weapon.Cooldown,
                    MoveSpeed = Speeds.TryGetComponent(entity, out var speed) ? speed.Value : 0f,
                    BuildRate = Builders.TryGetComponent(entity, out var builder) ? builder.Rate : 0f,
                    ProductionSpeed = Producers.TryGetComponent(entity, out var producer) ? producer.Speed : 1f,
                };
            }
        }
    }
}
