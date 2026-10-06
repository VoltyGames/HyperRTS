using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Runs Move, AttackMove and Patrol orders: points locomotion at the order goal while combat isn't engaged, and
    /// completes the order on arrival or when the unit has stalled close to its goal (crowded formations). A finished
    /// patrol leg goes to the back of the queue, so a unit with other legs queued loops through them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(OrderDispatchSystem))]
    public partial struct MoveOrderSystem : ISystem
    {
        /// <summary>Seconds without real movement before a unit near its goal counts as arrived.</summary>
        public const float StallSeconds = 2f;

        private ComponentLookup<AttackTarget> _attacks;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _attacks = state.GetComponentLookup<AttackTarget>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _attacks.Update(ref state);
            new MoveOrderJob { Attacks = _attacks, DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(MoveOrderState), typeof(MoveDestination))]
        private partial struct MoveOrderJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<AttackTarget> Attacks;
            public float DeltaTime;

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                ref MoveOrderState progress, EnabledRefRW<MoveOrderState> driving, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, in LocalTransform transform, in NavAgent agent,
                DynamicBuffer<QueuedOrder> queue)
            {
                if (!order.Value.Type.UsesFormation())
                {
                    return;
                }

                // Combat owns movement while engaged; re-apply the goal once it lets go.
                if (Attacks.HasEnabled(entity))
                {
                    driving.ValueRW = false;
                    return;
                }

                var position = transform.Position;
                if (!driving.ValueRO)
                {
                    destination.Value = order.Value.Position;
                    moving.ValueRW = true;
                    driving.ValueRW = true;
                    progress = new MoveOrderState { StallAnchor = position };
                    return;
                }

                var stalled = IsStalled(ref progress, position, order.Value.Position, agent.Radius);

                // A disabled destination means locomotion arrived, or got as close as the grid allows.
                if (!moving.ValueRO || stalled)
                {
                    ActiveOrder.Finish(busy, moving);
                    driving.ValueRW = false;
                    if (order.Value.Type == OrderType.Patrol && queue.Length > 0)
                    {
                        queue.Add(new QueuedOrder { Value = order.Value });
                    }
                }
            }

            private bool IsStalled(ref MoveOrderState progress, float3 position, float3 goal, float radius)
            {
                if (math.distancesq(position.xz, progress.StallAnchor.xz) > radius * radius * 0.25f)
                {
                    progress = new MoveOrderState { StallAnchor = position };
                    return false;
                }

                progress.StallTime += DeltaTime;
                var near = math.distance(position.xz, goal.xz) <= NavTolerances.Crowded(radius);
                return near && progress.StallTime >= StallSeconds;
            }
        }
    }
}
