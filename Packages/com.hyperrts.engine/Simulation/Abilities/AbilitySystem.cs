using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Production;
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
    public partial struct AbilitySystem : ISystem
    {
        private EntityQuery _completed;
        private TargetLookup _targets;
        private ComponentLookup<Faction> _factions;
        private AbilityCaster _caster;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _targets = new TargetLookup(ref state);
            _factions = state.GetComponentLookup<Faction>(true);
            _caster = new AbilityCaster(ref state);
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
            _caster.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>(),
                SystemAPI.GetSingletonEntity<AbilityEvents>());

            new CooldownJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();

            var completed = new CompletedBuildings(_completed, state.WorldUpdateAllocator, state.Dependency,
                out var gathered);
            state.Dependency = gathered;
            new CastJob
            {
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Completed = completed,
                Targets = _targets,
                Factions = _factions,
                Caster = _caster,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();
        }

        [BurstCompile]
        [WithNone(typeof(ConstructionProgress), typeof(Unpowered))]
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
            [ReadOnly] public CompletedBuildings Completed;
            public TargetLookup Targets;
            [ReadOnly] public ComponentLookup<Faction> Factions;
            public AbilityCaster Caster;
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
                if (index < 0 || !IsUsable(abilities[index], target, faction))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                var aim = AbilityRules.Aim(abilities[index], transform.Position, target, order.Value.Position, Targets);
                if (!AbilityRules.InRange(abilities[index], entity, transform.Position, target, aim, Targets))
                {
                    ReachMath.MoveTo(ref destination, moving, aim);
                    return;
                }

                ActiveOrder.Finish(busy, moving);
                Caster.Cast(ref abilities.ElementAt(index), entity, faction, target, aim, Ecb);
            }

            private bool IsUsable(in Ability ability, Entity target, byte faction) =>
                AbilityRules.IsUsable(ability, target, faction, Completed, Targets, Factions, Relations);
        }
    }
}
