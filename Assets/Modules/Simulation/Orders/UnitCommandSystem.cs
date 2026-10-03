using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Turns unit <see cref="PlayerCommand"/>s into orders for the commanded unit or the player's selected units.
    /// Ground moves of several units are spread into a <see cref="Formation"/>. Producers are not handled here.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(SelectionSystem))]
    public partial struct UnitCommandSystem : ISystem
    {
        private OrderWriter _writer;
        private OrderResolver _resolver;
        private ComponentLookup<Faction> _factions;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<NavAgent> _agents;
        private ComponentLookup<CombatStance> _stances;
        private EntityQuery _selected;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _writer = new OrderWriter(ref state);
            _resolver = new OrderResolver(ref state);
            _factions = state.GetComponentLookup<Faction>(true);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _stances = state.GetComponentLookup<CombatStance>();
            _selected = SystemAPI.QueryBuilder().WithAll<Selected, Faction>().WithPresent<ActiveOrder>()
                .WithNone<Dead>().Build();
            state.RequireForUpdate<PlayerCommand>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _writer.Update(ref state);
            _resolver.Update(ref state);
            _factions.Update(ref state);
            _transforms.Update(ref state);
            _agents.Update(ref state);
            _stances.Update(ref state);

            SystemAPI.TryGetSingleton<FactionRelations>(out var relations);
            var hasMap = SystemAPI.TryGetSingleton<MapSettings>(out var map);
            var subjects = new NativeList<Entity>(64, Allocator.Temp);
            foreach (var (player, commands) in SystemAPI.Query<RefRO<Player>, DynamicBuffer<PlayerCommand>>())
            {
                for (var i = 0; i < commands.Length; i++)
                {
                    var command = commands[i];
                    if (!IsUnitCommand(command.Type) || !GatherSubjects(player.ValueRO.Faction, command.Unit, subjects))
                    {
                        continue;
                    }

                    if (hasMap)
                    {
                        command.Position = map.Clamp(command.Position);
                    }

                    Execute(command, subjects.AsArray(), relations, hasMap, map);
                }
            }
        }

        private static bool IsUnitCommand(CommandType type) => type is >= CommandType.Smart and <= CommandType.SetStance;

        private bool GatherSubjects(byte faction, Entity unit, NativeList<Entity> subjects)
        {
            subjects.Clear();
            if (unit != Entity.Null)
            {
                if (_writer.CanReceiveOrders(unit) && _factions.TryGetComponent(unit, out var owner) &&
                    owner.Value == faction)
                {
                    subjects.Add(unit);
                }

                return subjects.Length > 0;
            }

            var selected = _selected.ToEntityArray(Allocator.Temp);
            foreach (var entity in selected)
            {
                if (_factions[entity].Value == faction)
                {
                    subjects.Add(entity);
                }
            }

            selected.Dispose();
            return subjects.Length > 0;
        }

        private void Execute(in PlayerCommand command, NativeArray<Entity> subjects, in FactionRelations relations,
            bool hasMap, in MapSettings map)
        {
            switch (command.Type)
            {
                case CommandType.Stop:
                    foreach (var unit in subjects)
                    {
                        _writer.Stop(unit);
                    }

                    return;
                case CommandType.SetStance:
                    SetStance(subjects, (Stance)command.Argument);
                    return;
            }

            var movers = new NativeList<Entity>(subjects.Length, Allocator.Temp);
            var moveTypes = new NativeList<OrderType>(subjects.Length, Allocator.Temp);
            foreach (var unit in subjects)
            {
                var type = _resolver.Resolve(command.Type, unit, command.Target, relations);
                if (type is OrderType.Move or OrderType.AttackMove)
                {
                    movers.Add(unit);
                    moveTypes.Add(type);
                }
                else
                {
                    var order = new Order { Type = type, Position = command.Position, Target = command.Target };
                    _writer.Issue(unit, order, command.Queue);
                }
            }

            IssueFormation(command, movers.AsArray(), moveTypes.AsArray(), hasMap, map);
            movers.Dispose();
            moveTypes.Dispose();
        }

        private void IssueFormation(in PlayerCommand command, NativeArray<Entity> movers, NativeArray<OrderType> types,
            bool hasMap, in MapSettings map)
        {
            if (movers.Length == 0)
            {
                return;
            }

            var positions = new NativeArray<float3>(movers.Length, Allocator.Temp);
            var slots = new NativeArray<float3>(movers.Length, Allocator.Temp);
            var radius = 0f;
            for (var i = 0; i < movers.Length; i++)
            {
                positions[i] = _transforms[movers[i]].Position;
                radius = math.max(radius, _agents.TryGetComponent(movers[i], out var agent) ? agent.Radius : 0.5f);
            }

            Formation.Assign(positions, command.Position, radius * 2.5f, slots);
            for (var i = 0; i < movers.Length; i++)
            {
                var slot = hasMap ? map.Clamp(slots[i]) : slots[i];
                _writer.Issue(movers[i], new Order { Type = types[i], Position = slot }, command.Queue);
            }

            positions.Dispose();
            slots.Dispose();
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
