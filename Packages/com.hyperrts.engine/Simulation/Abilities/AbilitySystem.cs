using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// Ticks every ability cooldown and runs UseAbility orders: the caster walks into range of its point or entity
    /// target, then fires. The order ends on firing, or when the ability or target is no longer usable.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(EngagementSystem))]
    [UpdateBefore(typeof(DamageSystem))]
    public partial struct AbilitySystem : ISystem
    {
        private TargetLookup _targets;
        private ComponentLookup<Faction> _factions;
        private AbilityActivator _activator;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _factions = state.GetComponentLookup<Faction>(true);
            _activator = new AbilityActivator(ref state);
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<AbilityEvents>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state);
            _factions.Update(ref state);
            _activator.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>(),
                SystemAPI.GetSingletonEntity<AbilityEvents>());

            new CooldownJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
            new CastJob
            {
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Targets = _targets,
                Factions = _factions,
                Activator = _activator,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();
        }

        [BurstCompile]
        private partial struct CooldownJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(DynamicBuffer<Ability> abilities)
            {
                for (var i = 0; i < abilities.Length; i++)
                {
                    ref var ability = ref abilities.ElementAt(i);
                    if (ability.CooldownRemaining > 0f)
                    {
                        ability.CooldownRemaining -= DeltaTime;
                    }
                }
            }
        }

        /// <summary>Single-threaded: casts append to the shared damage and event queues.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination))]
        private partial struct CastJob : IJobEntity
        {
            public FactionRelations Relations;
            public TargetLookup Targets;
            [ReadOnly] public ComponentLookup<Faction> Factions;
            public AbilityActivator Activator;
            public EntityCommandBuffer Ecb;

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                DynamicBuffer<Ability> abilities, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, in LocalTransform transform)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.UseAbility)
                {
                    return;
                }

                var faction = Factions[entity].Value;
                var index = AbilityRules.IndexOf(abilities, order.Value.Argument);
                var target = order.Value.Target;
                if (index < 0 || !abilities[index].IsReady ||
                    !AbilityRules.IsValidTarget(abilities[index], target, faction, Targets, Factions, Relations))
                {
                    busy.ValueRW = false;
                    moving.ValueRW = false;
                    return;
                }

                var aim = AbilityRules.Aim(abilities[index], transform.Position, target, order.Value.Position, Targets);
                if (!AbilityRules.InRange(abilities[index], entity, transform.Position, target, aim, Targets))
                {
                    ReachMath.MoveTo(ref destination, moving, aim);
                    return;
                }

                moving.ValueRW = false;
                busy.ValueRW = false;
                Activator.Activate(ref abilities.ElementAt(index), entity, faction, target, aim, Ecb);
            }
        }
    }
}
