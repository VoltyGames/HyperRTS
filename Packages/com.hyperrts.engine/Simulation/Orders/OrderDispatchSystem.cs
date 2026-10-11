using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Starts the next <see cref="QueuedOrder"/> on units whose <see cref="ActiveOrder"/> has finished.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct OrderDispatchSystem : ISystem
    {
        private OrderHalt _halt;

        public void OnCreate(ref SystemState state) => _halt = new OrderHalt(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _halt.Update(ref state);
            new DispatchJob { Halt = _halt }.ScheduleParallel();
        }

        [BurstCompile]
        [WithDisabled(typeof(ActiveOrder))]
        private partial struct DispatchJob : IJobEntity
        {
            // Writes only the entity being processed.
            public OrderHalt Halt;

            private void Execute(Entity entity, ref ActiveOrder active, EnabledRefRW<ActiveOrder> busy,
                DynamicBuffer<QueuedOrder> queue)
            {
                if (queue.Length == 0)
                {
                    return;
                }

                // Same reset as a fresh order, so a target picked up while idle can't hold the next order back.
                Halt.Apply(entity);
                active.Value = queue[0].Value;
                queue.RemoveAt(0);
                busy.ValueRW = true;
            }
        }
    }
}
