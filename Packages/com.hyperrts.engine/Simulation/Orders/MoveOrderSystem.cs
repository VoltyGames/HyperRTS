using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Agents;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Runs orders with <see cref="OrderTraits.MovesToPosition"/>: points locomotion at the goal while combat isn't
    /// engaged, and completes the order on <see cref="Arrival"/> (at the goal, against an arrived groupmate, or
    /// stalled), once turned to its <see cref="Order.Facing"/> if it has one. A faced unit turns only once its whole
    /// <see cref="Order.Group"/> has arrived, so no groupmate drives through its lane afterwards. A finished
    /// <see cref="OrderTraits.Loops"/> order goes to the back of the queue.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(OrderDispatchSystem))]
    public partial struct MoveOrderSystem : ISystem
    {
        /// <summary>Radians off its ordered facing an arrived unit may be and finish the order.</summary>
        public const float FacingTolerance = 0.05f;

        // A turn a weapon keeps overriding, or a hull that can't turn, gives up after this long.
        private const float MaxTurnSeconds = 10f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var traits = SystemAPI.GetSingleton<OrderTraitTable>();
            var unarrived = new NativeHashSet<int>(16, state.WorldUpdateAllocator);
            new UnarrivedGroupsJob { Groups = unarrived, Traits = traits }.Schedule();
            new MoveOrderJob
            {
                Attacks = SystemAPI.GetComponentLookup<AttackTarget>(true),
                UnarrivedGroups = unarrived,
                Traits = traits,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        /// <summary>Collects the formation groups with a unit still driving to its slot.</summary>
        [BurstCompile]
        [WithAll(typeof(MoveOrderState))]
        [WithDisabled(typeof(Arrival))]
        private partial struct UnarrivedGroupsJob : IJobEntity
        {
            public NativeHashSet<int> Groups;
            public OrderTraitTable Traits;

            private void Execute(in ActiveOrder order)
            {
                if (order.Value.Group != 0 && Traits.Has(order.Value.Type, OrderTraits.MovesToPosition))
                {
                    Groups.Add(order.Value.Group);
                }
            }
        }

        [BurstCompile]
        [WithPresent(typeof(MoveOrderState), typeof(MoveDestination), typeof(Arrival), typeof(FacingGoal))]
        private partial struct MoveOrderJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<AttackTarget> Attacks;
            [ReadOnly] public NativeHashSet<int> UnarrivedGroups;
            public OrderTraitTable Traits;
            public float DeltaTime;

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                ref MoveOrderState drive, EnabledRefRW<MoveOrderState> driving, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, EnabledRefRW<Arrival> arrived,
                DynamicBuffer<QueuedOrder> queue, in LocalTransform transform, ref FacingGoal goal,
                EnabledRefRW<FacingGoal> aiming)
            {
                if (!Traits.Has(order.Value.Type, OrderTraits.MovesToPosition))
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
                    // Turning before the group has arrived leaves a lane a groupmate would push it out of.
                    var waiting = math.any(order.Value.Facing != 0f) && UnarrivedGroups.Contains(order.Value.Group);
                    if (!waiting && HasTurned(order.Value.Facing, ref drive, transform, ref goal, aiming))
                    {
                        Finish(order.Value, busy, driving, moving, queue);
                    }

                    return;
                }

                // Not yet driving, or something else halted the walk: point locomotion at the goal again.
                if (!driving.ValueRO || !moving.ValueRO)
                {
                    destination = Goal(order.Value);
                    moving.ValueRW = true;
                    arrived.ValueRW = false;
                    driving.ValueRW = true;
                    drive.Turned = 0f;
                }
            }

            private readonly MoveDestination Goal(in Order order) => new()
            {
                Value = order.Position, Group = order.Group, Extent = order.Extent,
                Backward = Traits.Has(order.Type, OrderTraits.DrivesBackward),
            };

            /// <summary>
            /// True once the unit faces <paramref name="facing"/> (or has none), or has turned too long; until then
            /// it asks movement, which clears the goal each frame, to turn it.
            /// </summary>
            private readonly bool HasTurned(float2 facing, ref MoveOrderState drive, in LocalTransform transform,
                ref FacingGoal goal, EnabledRefRW<FacingGoal> aiming)
            {
                if (math.all(facing == 0f))
                {
                    return true;
                }

                var aligned = math.dot(Rotations.Facing(transform.Rotation), facing) >= math.cos(FacingTolerance);
                if (aligned || drive.Turned >= MaxTurnSeconds)
                {
                    return true;
                }

                drive.Turned += DeltaTime;
                // Still enabled here means combat aimed a weapon after last frame's movement: a target outranks a pose.
                if (!aiming.ValueRO)
                {
                    goal.Direction = facing;
                    aiming.ValueRW = true;
                }

                return false;
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
