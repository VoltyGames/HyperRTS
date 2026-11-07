using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// Handles UseAbility (self-targeted: every ready caster; aimed: the nearest ready one, which walks into range or,
    /// if it can't move, fires when in range) and UsePower (the player's own abilities). Clears last frame's events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct AbilityCommandSystem : ISystem
    {
        private CommandReader _commands;
        private EntityQuery _completed;
        private OrderWriter _writer;
        private TargetLookup _targets;
        private TargetQueries _targetQueries;
        private AbilityCaster _caster;

        public void OnCreate(ref SystemState state)
        {
            EventQueues.Create<AbilityEvents>(ref state, typeof(AbilityActivation));
            var selected = SystemAPI.QueryBuilder().WithAll<Ability, AbilityCooldown, Selected, Faction>().WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, TypeSet.Range(CommandType.UseAbility, CommandType.UsePower),
                selected);
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _writer = new OrderWriter(ref state);
            _targets = new TargetLookup(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            _caster = new AbilityCaster(ref state);
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingletonBuffer<AbilityActivation>().Clear();
            var issued = _commands.Read(ref state);
            if (issued.IsEmpty)
            {
                return;
            }

            state.CompleteDependency();
            _writer.Update(ref state, SystemAPI.GetSingleton<OrderTraitTable>());
            _targets.Update(ref state, _targetQueries);
            _caster.Update(ref state);

            var context = new Context
            {
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Completed = new CompletedBuildings(_completed, Allocator.Temp),
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            };
            var map = SystemAPI.GetSingleton<MapSettings>();
            foreach (var each in issued)
            {
                var aimed = each;
                aimed.Command.Position = map.Clamp(each.Command.Position);
                if (aimed.Command.Type == CommandType.UseAbility)
                {
                    UseAbility(ref state, aimed, context);
                }
                else
                {
                    UsePower(ref state, aimed.Player, aimed.Faction, aimed.Command, context);
                }
            }
        }

        private struct Context
        {
            public FactionRelations Relations;
            public CompletedBuildings Completed;
            public EntityCommandBuffer Ecb;
        }

        private void UseAbility(ref SystemState state, in IssuedCommand issued, in Context context)
        {
            var faction = issued.Faction;
            var command = issued.Command;
            var closest = Closest.None;
            foreach (var caster in _commands.Subjects(ref state, issued))
            {
                var index = UsableIndex(ref state, caster, faction, command, context);
                if (index < 0)
                {
                    continue;
                }

                var ability = SystemAPI.GetBuffer<Ability>(caster)[index];
                var from = SystemAPI.GetComponent<LocalTransform>(caster).Position;
                if (ability.Target == AbilityTarget.None)
                {
                    Cast(ref state, caster, index, faction, command, context);
                    continue;
                }

                var distance = math.distancesq(from.xz, AbilityRules.Aim(ability, from, command.Target, command.Position, _targets).xz);
                closest.Consider(index, caster, distance);
            }

            if (closest.Found)
            {
                Cast(ref state, closest.Entity, closest.Index, faction, command, context);
            }
        }

        /// <summary>The ability's slot when this caster may use it on the command's target now; otherwise -1.</summary>
        private int UsableIndex(ref SystemState state, Entity caster, byte faction, in PlayerCommand command,
            in Context context)
        {
            if (!CanCast(ref state, caster))
            {
                return -1;
            }

            var abilities = SystemAPI.GetBuffer<Ability>(caster);
            var index = AbilityRules.FindAbility(abilities, command.Argument);
            if (index < 0)
            {
                return -1;
            }

            var states = SystemAPI.GetBuffer<AbilityCooldown>(caster);
            var stateIndex = AbilityRules.FindCooldown(states, command.Argument);
            return stateIndex >= 0 && IsUsable(ref state, abilities[index], states[stateIndex], faction, command, context)
                ? index
                : -1;
        }

        /// <summary>A caster that is finished and powered.</summary>
        private bool CanCast(ref SystemState state, Entity caster) =>
            SystemAPI.HasBuffer<Ability>(caster) && AbilityRules.CanCast(state.EntityManager, caster);

        private bool IsUsable(ref SystemState state, in Ability ability, in AbilityCooldown abilityState, byte faction,
            in PlayerCommand command, in Context context) =>
            AbilityRules.IsAvailable(ability, abilityState, faction, context.Completed) &&
            AbilityRules.IsValidTarget(ability, command.Target, faction, _targets, SystemAPI.GetComponentLookup<Faction>(true),
                context.Relations);

        private void Cast(ref SystemState state, Entity caster, int index, byte faction, in PlayerCommand command,
            in Context context)
        {
            var abilities = SystemAPI.GetBuffer<Ability>(caster);
            var ability = abilities[index];
            if (ability.Target != AbilityTarget.None && _writer.CanReceiveOrders(caster))
            {
                var order = new Order
                {
                    Type = OrderType.UseAbility, Position = command.Position, Target = command.Target,
                    Argument = ability.Id,
                };
                _writer.Issue(caster, order, command.Queue);
                return;
            }

            var from = SystemAPI.GetComponent<LocalTransform>(caster).Position;
            var aim = AbilityRules.Aim(ability, from, command.Target, command.Position, _targets);
            if (AbilityRules.InRange(ability, caster, from, command.Target, aim, _targets))
            {
                var states = SystemAPI.GetBuffer<AbilityCooldown>(caster);
                var cooldown = AbilityRules.FindCooldown(states, ability.Id);
                _caster.Cast(ability, ref states.ElementAt(cooldown), Activation(ability, caster, faction, command.Target, aim),
                    context.Ecb);
            }
        }

        private void UsePower(ref SystemState state, Entity player, byte faction, in PlayerCommand command,
            in Context context)
        {
            if (!SystemAPI.HasBuffer<Ability>(player))
            {
                return;
            }

            var abilities = SystemAPI.GetBuffer<Ability>(player);
            var states = SystemAPI.GetBuffer<AbilityCooldown>(player);
            var index = AbilityRules.FindAbility(abilities, command.Argument);
            var stateIndex = AbilityRules.FindCooldown(states, command.Argument);
            if (index < 0 || stateIndex < 0 ||
                !IsUsable(ref state, abilities[index], states[stateIndex], faction, command, context))
            {
                return;
            }

            var aim = AbilityRules.Aim(abilities[index], command.Position, command.Target, command.Position, _targets);
            _caster.Cast(abilities[index], ref states.ElementAt(stateIndex),
                Activation(abilities[index], player, faction, command.Target, aim), context.Ecb);
        }

        private static AbilityActivation Activation(in Ability ability, Entity caster, byte faction, Entity target,
            float3 position) => new()
        {
            Caster = caster, Faction = faction, AbilityId = ability.Id, Target = target, Position = position,
        };
    }
}
