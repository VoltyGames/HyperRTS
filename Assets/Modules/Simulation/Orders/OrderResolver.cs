using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Picks the order a unit gets for a command from its capabilities and the target: Smart chooses
    /// attack/gather/build/move, explicit commands fall back to Move when the unit can't comply.
    /// </summary>
    public struct OrderResolver
    {
        private ComponentLookup<Faction> _factions;
        private TargetLookup _targets;
        private ComponentLookup<Weapon> _weapons;
        private ComponentLookup<Harvester> _harvesters;
        private ComponentLookup<ResourceNode> _nodes;
        private ComponentLookup<Builder> _builders;
        private ComponentLookup<ConstructionProgress> _construction;

        public OrderResolver(ref SystemState state)
        {
            _factions = state.GetComponentLookup<Faction>(true);
            _targets = new TargetLookup(ref state);
            _weapons = state.GetComponentLookup<Weapon>(true);
            _harvesters = state.GetComponentLookup<Harvester>(true);
            _nodes = state.GetComponentLookup<ResourceNode>(true);
            _builders = state.GetComponentLookup<Builder>(true);
            _construction = state.GetComponentLookup<ConstructionProgress>(true);
        }

        public void Update(ref SystemState state)
        {
            _factions.Update(ref state);
            _targets.Update(ref state);
            _weapons.Update(ref state);
            _harvesters.Update(ref state);
            _nodes.Update(ref state);
            _builders.Update(ref state);
            _construction.Update(ref state);
        }

        public OrderType Resolve(CommandType command, Entity unit, Entity target, in FactionRelations relations)
        {
            return command switch
            {
                CommandType.Smart => ResolveSmart(unit, target, relations),
                CommandType.Attack => CanAttack(unit, target, relations) ? OrderType.Attack : OrderType.Move,
                // Attack-move clicked on an enemy attacks it directly.
                CommandType.AttackMove => CanAttack(unit, target, relations) ? OrderType.Attack
                    : _weapons.HasComponent(unit) ? OrderType.AttackMove : OrderType.Move,
                CommandType.Gather => CanGather(unit, target) ? OrderType.Gather : OrderType.Move,
                CommandType.Build => CanBuild(unit, target, relations) ? OrderType.Build : OrderType.Move,
                _ => OrderType.Move,
            };
        }

        private OrderType ResolveSmart(Entity unit, Entity target, in FactionRelations relations)
        {
            if (CanAttack(unit, target, relations))
            {
                return OrderType.Attack;
            }

            if (CanGather(unit, target))
            {
                return OrderType.Gather;
            }

            return CanBuild(unit, target, relations) ? OrderType.Build : OrderType.Move;
        }

        private bool CanAttack(Entity unit, Entity target, in FactionRelations relations) =>
            _weapons.HasComponent(unit) && _targets.IsValidTarget(target, _factions[unit].Value, relations);

        private bool CanGather(Entity unit, Entity target) =>
            _harvesters.HasComponent(unit) && _nodes.HasComponent(target);

        private bool CanBuild(Entity unit, Entity target, in FactionRelations relations) =>
            _builders.HasComponent(unit) &&
            ConstructionRules.IsAlliedSite(_construction, _factions, relations, target, _factions[unit].Value);
    }
}
