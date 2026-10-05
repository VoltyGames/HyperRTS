using HyperRTS.Core;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Runs <see cref="OrderType.Attack"/> orders: points <see cref="AttackTarget"/> at the order's target and
    /// completes the order once that target is dead, gone or no longer hostile.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct AttackOrderSystem : ISystem
    {
        private TargetLookup _targets;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state);
            new AttackOrderJob
            {
                Targets = _targets,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(AttackTarget))]
        private partial struct AttackOrderJob : IJobEntity
        {
            public TargetLookup Targets;
            public FactionRelations Relations;

            private void Execute(ref ActiveOrder order, EnabledRefRW<ActiveOrder> hasOrder, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking, in Faction faction)
            {
                if (order.Value.Type != OrderType.Attack)
                {
                    return;
                }

                var target = order.Value.Target;
                if (!Targets.IsValidTarget(target, faction.Value, Relations))
                {
                    // EngagementSystem drops the stale AttackTarget and halts the chase.
                    hasOrder.ValueRW = false;
                    return;
                }

                if (!attacking.ValueRO || attack.Value != target)
                {
                    attack = new AttackTarget { Value = target };
                    attacking.ValueRW = true;
                }
            }
        }
    }
}
