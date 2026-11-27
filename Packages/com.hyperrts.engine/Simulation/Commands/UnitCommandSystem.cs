using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Commands
{
    /// <summary>
    /// Turns unit <see cref="PlayerCommand"/>s, and game commands registered in <see cref="OrderTraitTable"/>, into
    /// orders for the commanded unit or the player's selected units, spreading ground moves into a
    /// <see cref="Formation"/> on the <see cref="NavGrid"/>. Producers are handled elsewhere.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(SelectionSystem))]
    [UpdateBefore(typeof(OrderDispatchSystem))]
    [UpdateBefore(typeof(AbilityCommandSystem))]
    public partial struct UnitCommandSystem : ISystem
    {
        private OrderWriter _writer;
        private OrderResolver _resolver;
        private TargetQueries _targetQueries;
        private CommandReader _commands;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<NavAgent> _agents;
        private ComponentLookup<CombatStance> _stances;
        private BufferLookup<SquadMember> _members;

        // Last formation group handed out; 0 means no group.
        private int _lastGroup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _writer = new OrderWriter(ref state);
            _resolver = new OrderResolver(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            var selected = SystemAPI.QueryBuilder().WithAll<Selected, Owner>().WithPresent<ActiveOrder>().Build();
            _commands = new CommandReader(ref state, UnitCommands, selected);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _stances = state.GetComponentLookup<CombatStance>();
            _members = state.GetBufferLookup<SquadMember>(true);
            state.RequireForUpdate<PlayerCommand>();
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Most frames carry no commands; skip the sync with every job touching unit components.
            var issued = _commands.Read(ref state);
            if (issued.IsEmpty)
            {
                return;
            }

            state.CompleteDependency();
            _writer.Update(ref state, SystemAPI.GetSingleton<OrderTraitTable>());
            _resolver.Update(ref state, _targetQueries);
            _transforms.Update(ref state);
            _agents.Update(ref state);
            _stances.Update(ref state);
            _members.Update(ref state);

            var relations = SystemAPI.GetSingleton<OwnerRelations>();
            var map = SystemAPI.GetSingleton<MapSettings>();
            var grid = SystemAPI.GetSingleton<NavGrid>();
            var subjects = new NativeList<Entity>(64, Allocator.Temp);
            foreach (var each in issued)
            {
                if (!GatherSubjects(ref state, each, subjects))
                {
                    continue;
                }

                var command = each.Command;
                command.Position = map.Clamp(command.Position);
                Execute(command, subjects.AsArray(), relations, map, grid);
            }
        }

        /// <summary>Stop, stance, every engine command with a row in <see cref="CommandOrders"/>, and every game command.</summary>
        private static TypeSet UnitCommands
        {
            get
            {
                var set = TypeSet.Single(CommandType.Stop).With(CommandType.SetStance);
                // Game commands without a registered order resolve to none and are left to the game's systems.
                for (var type = 0; type <= byte.MaxValue; type++)
                {
                    var command = (CommandType)type;
                    if (command >= CommandType.GameFirst || CommandOrders.For(command).Choices.Length > 0)
                    {
                        set = set.With(command);
                    }
                }

                return set;
            }
        }

        /// <summary>Keeps the command's owned subjects that take orders, in order.</summary>
        private bool GatherSubjects(ref SystemState state, in IssuedCommand issued, NativeList<Entity> subjects)
        {
            _commands.Subjects(ref state, issued, subjects, new TakesOrders { Orders = _writer.Reader });
            return subjects.Length > 0;
        }

        private void Execute(in PlayerCommand command, NativeArray<Entity> subjects, in OwnerRelations relations,
            in MapSettings map, in NavGrid grid)
        {
            if (TryApplyDirectly(command, subjects))
            {
                return;
            }

            var movers = new NativeList<Entity>(subjects.Length, Allocator.Temp);
            var moveOrders = new NativeList<Order>(subjects.Length, Allocator.Temp);
            foreach (var unit in subjects)
            {
                var order = _resolver.Resolve(command, unit, relations, _writer.Traits);
                if (order.Type == OrderType.None)
                {
                    continue;
                }

                if (_writer.Traits.Has(order.Type, OrderTraits.Formation))
                {
                    movers.Add(unit);
                    moveOrders.Add(order);
                }
                else
                {
                    Issue(unit, order, command.Queue);
                }
            }

            IssueFormation(command, movers.AsArray(), moveOrders.AsArray(), map, grid);
        }

        /// <summary>Stop and stance changes need no order; true if the command was one of them.</summary>
        private bool TryApplyDirectly(in PlayerCommand command, NativeArray<Entity> subjects)
        {
            switch (command.Type)
            {
                case CommandType.Stop:
                    foreach (var unit in subjects)
                    {
                        _writer.Stop(unit);
                    }

                    return true;
                case CommandType.SetStance:
                    SetStance(subjects, (Stance)command.Argument);
                    return true;
                default:
                    return false;
            }
        }

        private void IssueFormation(in PlayerCommand command, NativeArray<Entity> movers, NativeArray<Order> orders,
            in MapSettings map, in NavGrid grid)
        {
            if (movers.Length == 0)
            {
                return;
            }

            var members = new NativeArray<FormationMember>(movers.Length, Allocator.Temp);
            for (var i = 0; i < movers.Length; i++)
            {
                members[i] = Member(movers[i]);
            }

            var slots = new NativeArray<float3>(movers.Length, Allocator.Temp);
            var extent = new FormationSlots(members).Assign(command.Position, command.Facing, map, grid, slots);
            _lastGroup++;
            for (var i = 0; i < movers.Length; i++)
            {
                var order = orders[i];
                order.Position = slots[i];
                order.Group = _lastGroup;
                order.Extent = extent;
                Issue(movers[i], order, command.Queue);
            }
        }

        private FormationMember Member(Entity unit)
        {
            var agent = _agents.TryGetComponent(unit, out var own) ? own : new NavAgent { Radius = EntityRadius.Default };
            var spread = _members.TryGetBuffer(unit, out var squad) ? new SquadRoster(squad).Spread() : 0f;
            return new FormationMember(_transforms[unit].Position, agent, spread);
        }

        private void Issue(Entity unit, in Order order, bool queue)
        {
            if (_writer.Traits.Has(order.Type, OrderTraits.Loops))
            {
                IssueLoop(unit, order, queue);
            }
            else
            {
                _writer.Issue(unit, order, queue);
            }
        }

        /// <summary>
        /// Looping orders such as Patrol run between the new point and where the unit would otherwise end up: here, or
        /// the end of its queue. Queued onto a route that already ends in a looping leg, the point joins that loop.
        /// </summary>
        private void IssueLoop(Entity unit, in Order leg, bool queue)
        {
            var start = _transforms[unit].Position;
            if (queue && _writer.Reader.TryGetLastOrder(unit, out var last))
            {
                if (_writer.Traits.Has(last.Type, OrderTraits.Loops))
                {
                    _writer.Issue(unit, leg, true);
                    return;
                }

                // Only ground moves end at their Position; other orders end wherever their target is.
                if (_writer.Traits.Has(last.Type, OrderTraits.MovesToPosition))
                {
                    start = last.Position;
                }
            }

            _writer.Issue(unit, leg, queue);
            _writer.Issue(unit, new Order { Type = leg.Type, Position = start, Argument = leg.Argument }, true);
        }

        private void SetStance(NativeArray<Entity> subjects, Stance stance)
        {
            foreach (var unit in subjects)
            {
                if (_stances.HasComponent(unit))
                {
                    _stances[unit] = _stances[unit].WithPost(stance, _transforms[unit].Position);
                }
            }
        }

        private struct TakesOrders : ISubjectFilter
        {
            public OrderReader Orders;

            public bool Keeps(Entity subject) => Orders.CanReceiveOrders(subject);
        }
    }
}
