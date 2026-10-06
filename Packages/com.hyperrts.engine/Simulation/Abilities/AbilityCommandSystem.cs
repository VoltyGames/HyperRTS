using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
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
    [UpdateAfter(typeof(UnitCommandSystem))]
    public partial struct AbilityCommandSystem : ISystem
    {
        private EntityQuery _selected;
        private EntityQuery _completed;
        private OrderWriter _writer;
        private TargetLookup _targets;
        private ComponentLookup<Faction> _factions;
        private AbilityActivator _activator;

        public void OnCreate(ref SystemState state)
        {
            var events = state.EntityManager.CreateEntity(typeof(AbilityEvents), typeof(AbilityActivation));
            state.EntityManager.SetName(events, "AbilityEvents");

            _selected = SystemAPI.QueryBuilder().WithAll<Ability, Selected, Faction>().WithNone<Dead>().Build();
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _writer = new OrderWriter(ref state);
            _targets = new TargetLookup(ref state);
            _factions = state.GetComponentLookup<Faction>(true);
            _activator = new AbilityActivator(ref state);
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonEntity<AbilityEvents>();
            SystemAPI.GetBuffer<AbilityActivation>(events).Clear();
            if (!HasAbilityCommand(ref state))
            {
                return;
            }

            state.CompleteDependency();
            _writer.Update(ref state);
            _targets.Update(ref state);
            _factions.Update(ref state);
            _activator.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>(), events);

            var context = new Context
            {
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Completed = new CompletedBuildings(_completed, Allocator.Temp),
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                HasMap = SystemAPI.TryGetSingleton<MapSettings>(out var map),
                Map = map,
            };
            Dispatch(ref state, context);
        }

        private void Dispatch(ref SystemState state, in Context context)
        {
            foreach (var (player, commands, entity) in SystemAPI.Query<RefRO<Player>, DynamicBuffer<PlayerCommand>>()
                         .WithNone<Defeated>().WithEntityAccess())
            {
                foreach (var command in commands)
                {
                    var aimed = command;
                    if (context.HasMap)
                    {
                        aimed.Position = context.Map.Clamp(command.Position);
                    }

                    if (command.Type == CommandType.UseAbility)
                    {
                        UseAbility(ref state, player.ValueRO.Faction, aimed, context);
                    }
                    else if (command.Type == CommandType.UsePower)
                    {
                        UsePower(ref state, entity, player.ValueRO.Faction, aimed, context);
                    }
                }
            }
        }

        private struct Context
        {
            public FactionRelations Relations;
            public CompletedBuildings Completed;
            public EntityCommandBuffer Ecb;
            public bool HasMap;
            public MapSettings Map;
        }

        private bool HasAbilityCommand(ref SystemState state)
        {
            foreach (var commands in SystemAPI.Query<DynamicBuffer<PlayerCommand>>())
            {
                foreach (var command in commands)
                {
                    if (command.Type is CommandType.UseAbility or CommandType.UsePower)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void UseAbility(ref SystemState state, byte faction, in PlayerCommand command, in Context context)
        {
            var best = Entity.Null;
            var bestIndex = -1;
            var bestDistance = float.MaxValue;
            foreach (var caster in CommandSubjects.Collect(command.Unit, _selected))
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
                if (distance < bestDistance || (distance == bestDistance && caster.Index < best.Index))
                {
                    best = caster;
                    bestIndex = index;
                    bestDistance = distance;
                }
            }

            if (best != Entity.Null)
            {
                Cast(ref state, best, bestIndex, faction, command, context);
            }
        }

        /// <summary>The ability's slot when this caster may use it on the command's target now; otherwise -1.</summary>
        private int UsableIndex(ref SystemState state, Entity caster, byte faction, in PlayerCommand command,
            in Context context)
        {
            if (!SystemAPI.HasBuffer<Ability>(caster) || SystemAPI.GetComponent<Faction>(caster).Value != faction ||
                ConstructionRules.IsUnderConstruction(state.EntityManager, caster) || IsUnpowered(ref state, caster))
            {
                return -1;
            }

            var abilities = SystemAPI.GetBuffer<Ability>(caster);
            var index = AbilityRules.IndexOf(abilities, command.Argument);
            return index >= 0 && IsUsable(abilities[index], faction, command, context) ? index : -1;
        }

        private bool IsUsable(in Ability ability, byte faction, in PlayerCommand command, in Context context) =>
            ability.IsReady && (ability.RequiredTypeId == 0 || context.Completed.Owns(faction, ability.RequiredTypeId)) &&
            AbilityRules.IsValidTarget(ability, command.Target, faction, _targets, _factions, context.Relations);

        private bool IsUnpowered(ref SystemState state, Entity entity) =>
            SystemAPI.HasComponent<Unpowered>(entity) && SystemAPI.IsComponentEnabled<Unpowered>(entity);

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
                _activator.Activate(ref abilities.ElementAt(index), caster, faction, command.Target, aim, context.Ecb);
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
            _activator.Activate(ref abilities.ElementAt(index), player, faction, command.Target, aim, context.Ecb);
        }
    }
}
