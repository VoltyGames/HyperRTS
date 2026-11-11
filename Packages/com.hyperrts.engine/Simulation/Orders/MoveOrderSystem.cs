using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Runs orders with <see cref="OrderTraits.MovesToPosition"/>: points locomotion at the goal while combat isn't
    /// engaged, and completes the order on <see cref="Arrival"/> (at the goal, against an arrived groupmate, or
    /// stalled). A finished <see cref="OrderTraits.Loops"/> order goes to the back of the queue.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(OrderDispatchSystem))]
    public partial struct MoveOrderSystem : ISystem
    {

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var traits = SystemAPI.GetSingleton<OrderTraitTable>();
            new MoveOrderJob
            {
                Attacks = SystemAPI.GetComponentLookup<AttackTarget>(true),
                Traits = traits,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(MoveOrderState), typeof(MoveDestination), typeof(Arrival))]
        private partial struct MoveOrderJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<AttackTarget> Attacks;
            public OrderTraitTable Traits;

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                EnabledRefRW<MoveOrderState> driving, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, EnabledRefRW<Arrival> arrived,
                DynamicBuffer<QueuedOrder> queue)
            {
                var type = order.Value.Type;
                if (!Traits.Has(type, OrderTraits.MovesToPosition))
                {
                    return;
                }

                // Combat owns movement while engaged; re-apply the goal once it lets go.
                if (Attacks.HasEnabled(entity))
                {
                    driving.ValueRW = false;
                    return;
                }

                if (driving.ValueRO && arrived.ValueRO)
                {
                    Finish(order.Value, busy, driving, moving, queue);
                    return;
                }

                // Not yet driving, or something else halted the walk: point locomotion at the goal again.
                if (!driving.ValueRO || !moving.ValueRO)
                {
                    destination = new MoveDestination
                    {
                        Value = order.Value.Position, Group = order.Value.Group, Extent = order.Value.Extent,
                        Backward = Traits.Has(type, OrderTraits.DrivesBackward),
                    };
                    moving.ValueRW = true;
                    arrived.ValueRW = false;
                    driving.ValueRW = true;
                }
            }

            private readonly void Finish(in Order order, EnabledRefRW<ActiveOrder> busy,
                EnabledRefRW<MoveOrderState> driving, EnabledRefRW<MoveDestination> moving,
                DynamicBuffer<QueuedOrder> queue)
            {
                ActiveOrder.Finish(busy, moving);
                driving.ValueRW = false;
                if (Traits.Has(order.Type, OrderTraits.Loops) && queue.Length > 0)
                {
                    queue.Add(new QueuedOrder { Value = order });
                }
            }
        }
    }
}
