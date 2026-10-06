using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>
    /// Rewrites the health cap from its base and the <see cref="StatModifier"/>s whenever they change; health keeps its
    /// fraction when the cap moves. Every other stat is applied by the module that owns its component.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct MaxHealthStatSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new ApplyJob().ScheduleParallel();

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        private partial struct ApplyJob : IJobEntity
        {
            private void Execute(ref Health health, in DynamicBuffer<StatModifier> modifiers,
                DynamicBuffer<BaseStat> bases)
            {
                var max = StatMath.Apply(modifiers, bases, Stat.MaxHealth, health.Max);
                health.Current = health.Max > 0f ? health.Current * max / health.Max : max;
                health.Max = max;
            }
        }
    }
}
