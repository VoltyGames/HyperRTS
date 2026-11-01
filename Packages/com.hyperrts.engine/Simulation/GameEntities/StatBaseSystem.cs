using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>
    /// Records the <see cref="BaseStat"/>s of newly spawned entities from their authored components, before any phase
    /// can write a live stat, so <see cref="StatSystem"/> always modifies the authored values.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct StatBaseSystem : ISystem
    {

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new RecordJob
            {
                Weapons = SystemAPI.GetComponentLookup<Weapon>(true),
                Speeds = SystemAPI.GetComponentLookup<MovementSpeed>(true),
                Visions = SystemAPI.GetComponentLookup<VisionRange>(true),
                Builders = SystemAPI.GetComponentLookup<Builder>(true),
                Producers = SystemAPI.GetComponentLookup<Producer>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct RecordJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<Weapon> Weapons;
            [ReadOnly] public ComponentLookup<MovementSpeed> Speeds;
            [ReadOnly] public ComponentLookup<VisionRange> Visions;
            [ReadOnly] public ComponentLookup<Builder> Builders;
            [ReadOnly] public ComponentLookup<Producer> Producers;

            // Modifiers are taken writable so StatSystem's change filter applies any added before the bases existed.
            private void Execute(Entity entity, EnabledRefRW<StatBasesPending> pending, in Health health,
                DynamicBuffer<BaseStat> bases, DynamicBuffer<StatModifier> modifiers)
            {
                bases.Clear();
                Record(bases, Stat.MaxHealth, health.Max);
                if (Weapons.TryGetComponent(entity, out var weapon))
                {
                    Record(bases, Stat.Damage, weapon.Damage);
                    Record(bases, Stat.Range, weapon.Range);
                    Record(bases, Stat.FireRate, weapon.Cooldown);
                }

                if (Speeds.TryGetComponent(entity, out var speed))
                {
                    Record(bases, Stat.MoveSpeed, speed.Value);
                }

                if (Visions.TryGetComponent(entity, out var vision))
                {
                    Record(bases, Stat.VisionRange, vision.Value);
                }

                if (Builders.TryGetComponent(entity, out var builder))
                {
                    Record(bases, Stat.BuildRate, builder.Rate);
                }

                if (Producers.TryGetComponent(entity, out var producer))
                {
                    Record(bases, Stat.ProductionSpeed, producer.Speed);
                }

                pending.ValueRW = false;
            }

            private static void Record(DynamicBuffer<BaseStat> bases, Stat stat, float value) =>
                bases.Add(new BaseStat { Stat = stat, Value = value });
        }
    }
}
