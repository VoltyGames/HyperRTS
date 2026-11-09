using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Status;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// Ticks ability cooldowns and runs UseAbility orders: the caster walks into range, then fires.
    /// Cooldowns hold while the caster is under construction or unpowered.
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
        private TargetQueries _targetQueries;
        private AbilityCaster _caster;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _targets = new TargetLookup(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            _caster = new AbilityCaster(ref state);
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _targetQueries);
            _caster.Update(ref state);

            new CooldownJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Cooldowns = SystemAPI.GetBufferLookup<AbilityCooldown>(),
            }.ScheduleParallel();

            var completed = new CompletedBuildings(_completed, state.WorldUpdateAllocator, state.Dependency,
                out var gathered);
            state.Dependency = gathered;
            new CastJob
            {
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                Completed = completed,
                Targets = _targets,
                Owners = SystemAPI.GetComponentLookup<Owner>(true),
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

            // Written only for the unit being run, and only while a cooldown runs, so idle chunks keep their version.
            [NativeDisableParallelForRestriction]
            [NativeDisableContainerSafetyRestriction]
            public BufferLookup<AbilityCooldown> Cooldowns;

            private void Execute(Entity entity, in DynamicBuffer<AbilityCooldown> read)
            {
                if (!IsRunning(read))
                {
                    return;
                }

                var states = Cooldowns[entity];
                for (var i = 0; i < states.Length; i++)
                {
                    ref var state = ref states.ElementAt(i);
                    if (state.Remaining > 0f)
                    {
                        state.Remaining -= DeltaTime;
                    }
                }
            }

            private static bool IsRunning(in DynamicBuffer<AbilityCooldown> states)
            {
                for (var i = 0; i < states.Length; i++)
                {
                    if (states[i].Remaining > 0f)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Single-threaded: casts append to the shared damage and event queues.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination))]
        private partial struct CastJob : IJobEntity
        {
            public OwnerRelations Relations;
            [ReadOnly] public CompletedBuildings Completed;
            public TargetLookup Targets;
            [ReadOnly] public ComponentLookup<Owner> Owners;
            public AbilityCaster Caster;
            public EntityCommandBuffer Ecb;

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                DynamicBuffer<Ability> abilities, DynamicBuffer<AbilityCooldown> states, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, in LocalTransform transform)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.UseAbility)
                {
                    return;
                }

                var owner = Owners[entity].Value;
                var index = AbilityRules.FindAbility(abilities, order.Value.Argument);
                var target = order.Value.Target;
                var stateIndex = index < 0 ? -1 : AbilityRules.FindCooldown(states, abilities[index].Id);
                if (stateIndex < 0 || !IsUsable(abilities[index], states[stateIndex], target, owner))
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
                Caster.Cast(abilities[index], ref states.ElementAt(stateIndex), new AbilityActivation
                {
                    Caster = entity, Owner = owner, AbilityId = abilities[index].Id, Target = target, Position = aim,
                }, Ecb);
            }

            private bool IsUsable(in Ability ability, in AbilityCooldown state, Entity target, byte owner) =>
                AbilityRules.IsAvailable(ability, state, owner, Completed) &&
                AbilityRules.IsValidTarget(ability, target, owner, Targets, Owners, Relations);
        }
    }
}
