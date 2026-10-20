using HyperRTS.Core;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
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
    /// Turns unit <see cref="PlayerCommand"/>s into orders for the commanded unit or the player's selected units.
    /// Ground moves of several units are spread into a <see cref="Formation"/>; a patrol also queues the leg back to
    /// where each unit stands or its queue ends. Producers are not handled here.
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
        private EntityQuery _fogQuery;
        private CommandReader _commands;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<NavAgent> _agents;
        private ComponentLookup<CombatStance> _stances;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _writer = new OrderWriter(ref state);
            _resolver = new OrderResolver(ref state);
            _fogQuery = TargetLookup.FogQuery(ref state);
            var selected = SystemAPI.QueryBuilder().WithAll<Selected, Faction>().WithPresent<ActiveOrder>()
                .WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, UnitCommands, selected);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _stances = state.GetComponentLookup<CombatStance>();
            state.RequireForUpdate<PlayerCommand>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<MapSettings>();
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
            _writer.Update(ref state);
            _resolver.Update(ref state, _fogQuery);
            _transforms.Update(ref state);
            _agents.Update(ref state);
            _stances.Update(ref state);

            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var map = SystemAPI.GetSingleton<MapSettings>();
            var subjects = new NativeList<Entity>(64, Allocator.Temp);
            foreach (var each in issued)
            {
                if (!GatherSubjects(ref state, each, subjects))
                {
                    continue;
                }

                var command = each.Command;
                command.Position = map.Clamp(command.Position);
                Execute(command, subjects.AsArray(), relations, map);
            }
        }

        private static TypeSet UnitCommands =>
            TypeSet.Range(CommandType.Smart, CommandType.SetStance)
                .With(TypeSet.Range(CommandType.Repair, CommandType.Enter))
                .With(TypeSet.Range(CommandType.Patrol, CommandType.ReturnToBase));

        /// <summary>Keeps the command's owned subjects that take orders, in order.</summary>
        private bool GatherSubjects(ref SystemState state, in IssuedCommand issued, NativeList<Entity> subjects)
        {
            _commands.Subjects(ref state, issued, subjects);
            var kept = 0;
            for (var i = 0; i < subjects.Length; i++)
            {
                var entity = subjects[i];
                if (_writer.CanReceiveOrders(entity))
                {
                    subjects[kept++] = entity;
                }
            }

            subjects.Length = kept;
            return kept > 0;
        }

        private void Execute(in PlayerCommand command, NativeArray<Entity> subjects, in FactionRelations relations,
            in MapSettings map)
        {
            if (TryApplyDirectly(command, subjects))
            {
                return;
            }

            var movers = new NativeList<Entity>(subjects.Length, Allocator.Temp);
            var moveOrders = new NativeList<Order>(subjects.Length, Allocator.Temp);
            foreach (var unit in subjects)
            {
                var order = _resolver.Resolve(command.Type, unit, command.Target, relations);
                if (order.Type == OrderType.None)
                {
                    continue;
                }

                if (order.Has(OrderTraits.Formation))
                {
                    movers.Add(unit);
                    moveOrders.Add(order);
                }
                else
                {
                    order.Position = command.Position;
                    order.Target = command.Target;
                    _writer.Issue(unit, order, command.Queue);
                }
            }

            IssueFormation(command, movers.AsArray(), moveOrders.AsArray(), map);
            movers.Dispose();
            moveOrders.Dispose();
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
            in MapSettings map)
        {
            if (movers.Length == 0)
            {
                return;
            }

            var positions = new NativeArray<float3>(movers.Length, Allocator.Temp);
            var slots = new NativeArray<float3>(movers.Length, Allocator.Temp);
            var spacing = float2.zero;
            for (var i = 0; i < movers.Length; i++)
            {
                positions[i] = _transforms[movers[i]].Position;
                spacing = math.max(spacing, SlotSpacing(movers[i]));
            }

            Formation.Assign(positions, command.Position, spacing, slots);
            for (var i = 0; i < movers.Length; i++)
            {
                var order = orders[i];
                order.Position = map.Clamp(slots[i]);
                if (order.Type == OrderType.Patrol)
                {
                    IssuePatrol(movers[i], order, positions[i], command.Queue);
                }
                else
                {
                    _writer.Issue(movers[i], order, command.Queue);
                }
            }

            positions.Dispose();
            slots.Dispose();
        }

        private float2 SlotSpacing(Entity unit)
        {
            var agent = _agents.TryGetComponent(unit, out var own) ? own : new NavAgent { Radius = EntityRadius.Default };
            return agent.SlotSpacing;
        }

        /// <summary>
        /// Patrols loop between the new point and where the unit would otherwise end up: here, or the end of its queue.
        /// Queued onto a route that already ends in a patrol leg, the point joins that loop instead.
        /// </summary>
        private void IssuePatrol(Entity unit, in Order leg, float3 position, bool queue)
        {
            var start = position;
            if (queue && _writer.TryGetLastOrder(unit, out var last))
            {
                if (last.Type == OrderType.Patrol)
                {
                    _writer.Issue(unit, leg, true);
                    return;
                }

                // Only ground moves end at their Position; other orders end wherever their target is.
                if (last.Has(OrderTraits.Formation))
                {
                    start = last.Position;
                }
            }

            _writer.Issue(unit, leg, queue);
            _writer.Issue(unit, new Order { Type = OrderType.Patrol, Position = start }, true);
        }

        private void SetStance(NativeArray<Entity> subjects, Stance stance)
        {
            foreach (var unit in subjects)
            {
                if (_stances.HasComponent(unit))
                {
                    _stances[unit] = new CombatStance { Value = stance, Anchor = _transforms[unit].Position };
                }
            }
        }
    }
}
