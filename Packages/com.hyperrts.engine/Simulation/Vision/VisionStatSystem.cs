using HyperRTS.Core;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Rewrites <see cref="VisionRange"/> from its base and the stat modifiers whenever they change.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct VisionStatSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new ApplyJob().ScheduleParallel();

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        private partial struct ApplyJob : IJobEntity
        {
            private void Execute(ref VisionRange vision, in DynamicBuffer<StatModifier> modifiers,
                DynamicBuffer<BaseStat> bases)
            {
                vision.Value = StatMath.Apply(modifiers, bases, Stat.VisionRange, vision.Value);
            }
        }
    }
}
