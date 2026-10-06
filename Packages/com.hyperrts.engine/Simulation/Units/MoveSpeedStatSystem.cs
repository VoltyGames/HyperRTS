using HyperRTS.Core;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Units
{
    /// <summary>Rewrites <see cref="MovementSpeed"/> from its base and the stat modifiers whenever they change.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct MoveSpeedStatSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new ApplyJob().ScheduleParallel();

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        private partial struct ApplyJob : IJobEntity
        {
            private void Execute(ref MovementSpeed speed, in DynamicBuffer<StatModifier> modifiers,
                DynamicBuffer<BaseStat> bases)
            {
                speed.Value = StatMath.Apply(modifiers, bases, Stat.MoveSpeed, speed.Value);
            }
        }
    }
}
