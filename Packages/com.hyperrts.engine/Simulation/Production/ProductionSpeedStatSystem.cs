using HyperRTS.Core;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Rewrites <see cref="Producer.Speed"/> from its base and the stat modifiers whenever they change.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct ProductionSpeedStatSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new ApplyJob().ScheduleParallel();

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        private partial struct ApplyJob : IJobEntity
        {
            private void Execute(ref Producer producer, in DynamicBuffer<StatModifier> modifiers,
                DynamicBuffer<BaseStat> bases)
            {
                producer.Speed = StatMath.Apply(modifiers, bases, Stat.ProductionSpeed, producer.Speed);
            }
        }
    }
}
