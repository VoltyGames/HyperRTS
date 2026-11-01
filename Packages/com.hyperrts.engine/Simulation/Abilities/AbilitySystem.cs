using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Status;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// Ticks ability cooldowns and runs UseAbility orders: the caster walks into range of its point or entity target,
    /// then fires. The order ends on firing, or when the ability, its required building or the target is no longer
    /// usable. Like weapons, cooldowns hold while the caster is under construction or unpowered, so a superweapon
    /// only starts charging once it is finished.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(EngagementSystem))]
    [UpdateBefore(typeof(DamageSystem))]
    [UpdateBefore(typeof(StatusEffectSystem))]
    public partial struct AbilitySystem : ISystem
    {
        private EntityQuery _completed;
        private TargetLookup _targets;
        private EntityQuery _fogQuery;
        private AbilityCaster _caster;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _targets = new TargetLookup(ref state);
            _fogQuery = TargetLookup.FogQuery(ref state);
            _caster = new AbilityCaster(ref state);
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _fogQuery);
            _caster.Update(ref state);

            new CooldownJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();

            var completed = new CompletedBuildings(_completed, state.WorldUpdateAllocator, state.Dependency,
                out var gathered);
            state.Dependency = gathered;
            new CastJob
            {
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Completed = completed,
                Targets = _targets,
                Factions = SystemAPI.GetComponentLookup<Faction>(true),
                Caster = _caster,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();
        }

        [BurstCompile]
        [WithNone(typeof(Offline))]
        private partial struct CooldownJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(DynamicBuffer<AbilityState> states)
            {
                for (var i = 0; i < states.Length; i++)
                {
                    ref var state = ref states.ElementAt(i);
                    if (state.CooldownRemaining > 0f)
                    {
                        state.CooldownRemaining -= DeltaTime;
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
            [ReadOnly] public CompletedBuildings Completed;
            public TargetLookup Targets;
            [ReadOnly] public ComponentLookup<Faction> Factions;
            public AbilityCaster Caster;
            public EntityCommandBuffer Ecb;

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                DynamicBuffer<Ability> abilities, DynamicBuffer<AbilityState> states, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, in LocalTransform transform)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.UseAbility)
                {
                    return;
                }

                var faction = Factions[entity].Value;
                var index = AbilityRules.FindAbility(abilities, order.Value.Argument);
                var target = order.Value.Target;
                var stateIndex = index < 0 ? -1 : AbilityRules.FindState(states, abilities[index].Id);
                if (stateIndex < 0 || !IsUsable(abilities[index], states[stateIndex], target, faction))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                var aim = AbilityRules.Aim(abilities[index], transform.Position, target, order.Value.Position, Targets);
                if (!AbilityRules.InRange(abilities[index], entity, transform.Position, target, aim, Targets))
                {
                    destination.MoveTo(moving, aim);
                    return;
                }

                ActiveOrder.Finish(busy, moving);
                Caster.Cast(abilities[index], ref states.ElementAt(stateIndex), entity, faction, target, aim, Ecb);
            }

            private bool IsUsable(in Ability ability, in AbilityState state, Entity target, byte faction) =>
                AbilityRules.IsUsable(ability, state, target, faction, Completed, Targets, Factions, Relations);
        }
    }
}
