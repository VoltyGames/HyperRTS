using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat
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
        private EntityQuery _fogQuery;
        private ComponentLookup<Ammo> _ammo;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _fogQuery = TargetLookup.FogQuery(ref state);
            _ammo = state.GetComponentLookup<Ammo>(true);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _fogQuery);
            _ammo.Update(ref state);
            new AttackOrderJob
            {
                Targets = _targets,
                AmmoLookup = _ammo,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(AttackTarget))]
        private partial struct AttackOrderJob : IJobEntity
        {
            public TargetLookup Targets;
            public FactionRelations Relations;
            [ReadOnly] public ComponentLookup<Ammo> AmmoLookup;

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> hasOrder, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking, in Faction faction, in Weapon weapon)
            {
                if (order.Value.Type != OrderType.Attack)
                {
                    return;
                }

                var target = order.Value.Target;
                var valid = Targets.IsValidTarget(target, faction.Value, Relations, weapon.Targets);
                if (!valid || Ammo.IsEmpty(AmmoLookup, entity))
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
