using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// Runs <see cref="OrderType.Attack"/> orders: points <see cref="AttackTarget"/> at the order's target and
    /// completes the order once that target is dead, gone, no longer hostile, hidden by stealth or out of the
    /// weapon's reach (an aircraft for a ground-only gun), or when the weapon runs out of ammo.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct AttackOrderSystem : ISystem
    {
        private TargetLookup _targets;
        private TargetQueries _targetQueries;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _targetQueries);
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

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> hasOrder, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking, in Faction faction, in DynamicBuffer<WeaponMount> mounts)
            {
                if (order.Value.Type != OrderType.Attack)
                {
                    return;
                }

                var target = order.Value.Target;
                var primary = mounts[0];
                var valid = Targets.IsAttackable(target, faction.Value, Relations, primary.Weapon.Targets);
                if (!valid || primary.Ammo.Empty)
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
