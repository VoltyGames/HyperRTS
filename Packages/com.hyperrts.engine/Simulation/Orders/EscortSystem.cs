using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Runs Escort orders: keeps the escort within <see cref="FollowDistance"/> of its ward while combat, which
    /// auto-acquires for escorts, fights off hostiles that come near. Ends when the ward dies.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(MoveOrderSystem))]
    public partial struct EscortSystem : ISystem
    {
        /// <summary>Edge-to-edge gap the escort closes before it stops following.</summary>
        public const float FollowDistance = 2f;


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new EscortJob
            {
                HealthLookup = SystemAPI.GetComponentLookup<Health>(true),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Agents = SystemAPI.GetComponentLookup<NavAgent>(true),
                Obstacles = SystemAPI.GetComponentLookup<NavObstacle>(true),
                Attacks = SystemAPI.GetComponentLookup<AttackTarget>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(MoveDestination))]
        private partial struct EscortJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<NavAgent> Agents;
            [ReadOnly] public ComponentLookup<NavObstacle> Obstacles;
            [ReadOnly] public ComponentLookup<AttackTarget> Attacks;

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, in LocalTransform transform,
                in NavAgent agent)
            {
                if (order.Value.Type != OrderType.Escort)
                {
                    return;
                }

                var ward = order.Value.Target;
                if (!Health.IsAlive(HealthLookup, ward))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                // Combat owns movement while engaged.
                if (Attacks.HasEnabled(entity))
                {
                    return;
                }

                var wardPosition = Transforms[ward].Position;
                var wardRadius = EntityRadius.Get(ward, Agents, Obstacles);
                var gap = EntityRadius.EdgeDistance(transform.Position, agent.Radius, wardPosition, wardRadius);
                if (gap > FollowDistance)
                {
                    destination.MoveTo(moving, wardPosition);
                }
                else
                {
                    moving.ValueRW = false;
                }
            }
        }
    }
}
