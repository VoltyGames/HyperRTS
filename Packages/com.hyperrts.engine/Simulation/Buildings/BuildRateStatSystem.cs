using HyperRTS.Core;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Rewrites <see cref="Builder.Rate"/> from its base and the stat modifiers whenever they change.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct BuildRateStatSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new ApplyJob().ScheduleParallel();

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        private partial struct ApplyJob : IJobEntity
        {
            private void Execute(ref Builder builder, in DynamicBuffer<StatModifier> modifiers,
                DynamicBuffer<BaseStat> bases)
            {
                builder.Rate = StatMath.Apply(modifiers, bases, Stat.BuildRate, builder.Rate);
            }
        }
    }
}
