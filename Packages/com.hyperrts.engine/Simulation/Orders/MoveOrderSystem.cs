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

        // About 20 degrees of turn restarts the stall timer.
        private const float TurnedCos = 0.94f;

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
                if (!order.Value.Has(OrderTraits.Formation))
                {
                    return;
                }

                // Combat owns movement while engaged; re-apply the goal once it lets go.
                if (Attacks.HasEnabled(entity))
                {
                    driving.ValueRW = false;
                    return;
                }

                if (!driving.ValueRO)
                {
                    destination.Value = order.Value.Position;
                    moving.ValueRW = true;
                    driving.ValueRW = true;
                    progress = Restart(transform);
                    return;
                }

                var stalled = IsStalled(ref progress, transform, order.Value.Position, agent);

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

            private bool IsStalled(ref MoveOrderState progress, in LocalTransform transform, float3 goal,
                in NavAgent agent)
            {
                var radius = agent.Radius;
                var position = transform.Position;
                var moved = math.distancesq(position.xz, progress.StallAnchor.xz) > radius * radius * 0.25f;
                var turned = math.dot(Facing(transform), progress.StallFacing) < TurnedCos;
                if (moved || turned)
                {
                    progress = Restart(transform);
                    return false;
                }

                progress.StallTime += DeltaTime;
                // Long or widely spaced footprints crowd the goal from further away.
                var near = math.distance(position.xz, goal.xz) <= NavTolerances.Crowded(agent.HalfLength + agent.Spacing);
                return near && progress.StallTime >= StallSeconds;
            }

            private static MoveOrderState Restart(in LocalTransform transform) =>
                new() { StallAnchor = transform.Position, StallFacing = Facing(transform) };

            private static float2 Facing(in LocalTransform transform) =>
                math.normalizesafe(math.mul(transform.Rotation, math.forward()).xz);
        }
    }
}
