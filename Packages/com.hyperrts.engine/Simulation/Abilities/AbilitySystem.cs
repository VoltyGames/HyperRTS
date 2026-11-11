using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Status;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
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
        private EntityQuery _cooling;
        private TargetLookup _targets;
        private TargetQueries _targetQueries;
        private AbilityCaster _caster;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _cooling = new EntityQueryBuilder(Allocator.Temp).WithAllRW<AbilityCooldown>().WithNone<Offline>()
                .Build(ref state);
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

            state.Dependency = new CooldownJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Cooldowns = SystemAPI.GetBufferTypeHandle<AbilityCooldown>(),
            }.ScheduleParallel(_cooling, state.Dependency);

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
        private struct CooldownJob : IJobChunk
        {
            public float DeltaTime;
            public BufferTypeHandle<AbilityCooldown> Cooldowns;

            // The writable accessor is taken only for a unit with a running cooldown, so idle chunks keep their version.
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var read = chunk.GetBufferAccessorRO(ref Cooldowns);
                var entities = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entities.NextEntityIndex(out var i))
                {
                    if (!IsRunning(read[i]))
                    {
                        continue;
                    }

                    Tick(chunk.GetBufferAccessorRW(ref Cooldowns)[i]);
                }
            }

            private void Tick(DynamicBuffer<AbilityCooldown> states)
            {
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
                var index = Ability.Find(abilities, order.Value.Argument);
                var target = order.Value.Target;
                var stateIndex = index < 0 ? -1 : AbilityCooldown.Find(states, abilities[index].Id);
                if (stateIndex < 0 || !IsUsable(abilities[index], states[stateIndex], target, owner))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                var aim = abilities[index].Aim(transform.Position, target, order.Value.Position, Targets);
                if (!abilities[index].InRange(entity, transform.Position, target, aim, Targets))
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
                ability.IsAvailable(state, owner, Completed) &&
                ability.IsValidTarget(target, owner, Targets, Owners, Relations);
        }
    }
}
