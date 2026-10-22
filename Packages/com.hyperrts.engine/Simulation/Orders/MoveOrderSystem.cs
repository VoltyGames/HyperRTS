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
    /// Runs orders with <see cref="OrderTraits.MovesToPosition"/>: points locomotion at the order goal while combat
    /// isn't engaged, and completes the order on arrival or when the unit has been stuck close to its goal (crowded
    /// formations). A finished <see cref="OrderTraits.Loops"/> order goes to the back of the queue.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(OrderDispatchSystem))]
    public partial struct MoveOrderSystem : ISystem
    {
        /// <summary>Seconds without real movement before a unit near its goal counts as arrived.</summary>
        public const float StuckSeconds = 2f;

        // About 20 degrees of turn restarts the stuck timer.
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
            var traits = SystemAPI.GetSingleton<OrderTraitTable>();
            new MoveOrderJob
            {
                Attacks = _attacks,
                Traits = traits,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(MoveOrderState), typeof(MoveDestination))]
        private partial struct MoveOrderJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<AttackTarget> Attacks;
            public OrderTraitTable Traits;
            public float DeltaTime;

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                ref MoveOrderState progress, EnabledRefRW<MoveOrderState> driving, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, in LocalTransform transform, in NavAgent agent,
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

                if (!driving.ValueRO)
                {
                    destination.Value = order.Value.Position;
                    moving.ValueRW = true;
                    driving.ValueRW = true;
                    progress = Restart(transform);
                    return;
                }

                var stuck = IsStuck(ref progress, transform, order.Value.Position, agent);

                // A disabled destination means locomotion arrived, or got as close as the grid allows.
                if (!moving.ValueRO || stuck)
                {
                    ActiveOrder.Finish(busy, moving);
                    driving.ValueRW = false;
                    if (Traits.Has(type, OrderTraits.Loops) && queue.Length > 0)
                    {
                        queue.Add(new QueuedOrder { Value = order.Value });
                    }
                }
            }

            private bool IsStuck(ref MoveOrderState progress, in LocalTransform transform, float3 goal,
                in NavAgent agent)
            {
                var radius = agent.Radius;
                var position = transform.Position;
                var moved = math.distancesq(position.xz, progress.StuckSpot.xz) > radius * radius * 0.25f;
                var turned = math.dot(Facing(transform), progress.StuckFacing) < TurnedCos;
                if (moved || turned)
                {
                    progress = Restart(transform);
                    return false;
                }

                progress.StuckTime += DeltaTime;
                // Long or widely spaced footprints crowd the goal from further away.
                var near = math.distance(position.xz, goal.xz) <= NavTolerances.Crowded(agent.HalfLength + agent.Spacing);
                return near && progress.StuckTime >= StuckSeconds;
            }

            private static MoveOrderState Restart(in LocalTransform transform) =>
                new() { StuckSpot = transform.Position, StuckFacing = Facing(transform) };

            private static float2 Facing(in LocalTransform transform) =>
                math.normalizesafe(math.mul(transform.Rotation, math.forward()).xz);
        }
    }
}
