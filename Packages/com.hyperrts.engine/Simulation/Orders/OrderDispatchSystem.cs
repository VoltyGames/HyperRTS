using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Starts the next <see cref="QueuedOrder"/> on units whose <see cref="ActiveOrder"/> has finished.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(UnitCommandSystem))]
    public partial struct OrderDispatchSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new DispatchJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithDisabled(typeof(ActiveOrder))]
        private partial struct DispatchJob : IJobEntity
        {
            private void Execute(ref ActiveOrder active, EnabledRefRW<ActiveOrder> busy, DynamicBuffer<QueuedOrder> queue)
            {
                if (queue.Length == 0)
                {
                    return;
                }

                active.Value = queue[0].Value;
                queue.RemoveAt(0);
                busy.ValueRW = true;
            }
        }
    }
}
