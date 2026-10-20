using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
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
        private EntityQuery _fogQuery;
        private ComponentLookup<Faction> _factions;
        private AbilityCaster _caster;

        public void OnCreate(ref SystemState state)
        {
            var events = state.EntityManager.CreateEntity(typeof(AbilityEvents), typeof(AbilityActivation));
            state.EntityManager.SetName(events, "AbilityEvents");

            var selected = SystemAPI.QueryBuilder().WithAll<Ability, Selected, Faction>().WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, TypeSet.Range(CommandType.UseAbility, CommandType.UsePower),
                selected);
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _writer = new OrderWriter(ref state);
            _targets = new TargetLookup(ref state);
            _fogQuery = TargetLookup.FogQuery(ref state);
            _factions = state.GetComponentLookup<Faction>(true);
            _caster = new AbilityCaster(ref state);
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonEntity<AbilityEvents>();
            SystemAPI.GetBuffer<AbilityActivation>(events).Clear();
            var issued = _commands.Read(ref state);
            if (issued.IsEmpty)
            {
                return;
            }

            state.CompleteDependency();
            _writer.Update(ref state);
            _targets.Update(ref state, _fogQuery);
            _factions.Update(ref state);
            _caster.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>(), events);

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
            var index = AbilityRules.IndexOf(abilities, command.Argument);
            return index >= 0 && IsUsable(abilities[index], faction, command, context) ? index : -1;
        }

        /// <summary>A caster that is finished and powered.</summary>
        private bool CanCast(ref SystemState state, Entity caster) =>
            SystemAPI.HasBuffer<Ability>(caster) && AbilityRules.CanCast(state.EntityManager, caster);

        private bool IsUsable(in Ability ability, byte faction, in PlayerCommand command, in Context context) =>
            AbilityRules.IsUsable(ability, command.Target, faction, context.Completed, _targets, _factions,
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
                _caster.Cast(ref abilities.ElementAt(index), caster, faction, command.Target, aim, context.Ecb);
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
            var index = AbilityRules.IndexOf(abilities, command.Argument);
            if (index < 0 || !IsUsable(abilities[index], faction, command, context))
            {
                return;
            }

            var aim = AbilityRules.Aim(abilities[index], command.Position, command.Target, command.Position, _targets);
            _caster.Cast(ref abilities.ElementAt(index), player, faction, command.Target, aim, context.Ecb);
        }
    }
}
