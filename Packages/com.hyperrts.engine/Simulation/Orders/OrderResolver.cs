using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Transport;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Picks the order a unit gets for a command from its capabilities and the target: Smart chooses
    /// attack/gather/build/repair/enter/capture/move, explicit commands fall back to Move when the unit can't comply.
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
        private ComponentLookup<Health> _health;
        private ComponentLookup<Capturer> _capturers;
        private ComponentLookup<Capturable> _capturables;
        private ComponentLookup<Passenger> _passengers;
        private ComponentLookup<Container> _containers;

        public OrderResolver(ref SystemState state)
        {
            _factions = state.GetComponentLookup<Faction>(true);
            _targets = new TargetLookup(ref state);
            _weapons = state.GetComponentLookup<Weapon>(true);
            _harvesters = state.GetComponentLookup<Harvester>(true);
            _nodes = state.GetComponentLookup<ResourceNode>(true);
            _builders = state.GetComponentLookup<Builder>(true);
            _construction = state.GetComponentLookup<ConstructionProgress>(true);
            _health = state.GetComponentLookup<Health>(true);
            _capturers = state.GetComponentLookup<Capturer>(true);
            _capturables = state.GetComponentLookup<Capturable>(true);
            _passengers = state.GetComponentLookup<Passenger>(true);
            _containers = state.GetComponentLookup<Container>(true);
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
            _health.Update(ref state);
            _capturers.Update(ref state);
            _capturables.Update(ref state);
            _passengers.Update(ref state);
            _containers.Update(ref state);
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
                CommandType.Repair => CanRepair(unit, target, relations) ? OrderType.Repair : OrderType.Move,
                CommandType.Capture => CanCapture(unit, target, relations) ? OrderType.Capture : OrderType.Move,
                CommandType.Enter => CanEnter(unit, target, relations) ? OrderType.Enter : OrderType.Move,
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

            if (CanBuild(unit, target, relations))
            {
                return OrderType.Build;
            }

            if (CanRepair(unit, target, relations))
            {
                return OrderType.Repair;
            }

            if (CanEnter(unit, target, relations))
            {
                return OrderType.Enter;
            }

            return CanCapture(unit, target, relations) ? OrderType.Capture : OrderType.Move;
        }

        private bool CanAttack(Entity unit, Entity target, in FactionRelations relations) =>
            _weapons.HasComponent(unit) && _targets.IsValidTarget(target, _factions[unit].Value, relations);

        private bool CanGather(Entity unit, Entity target) =>
            _harvesters.HasComponent(unit) && _nodes.HasComponent(target);

        private bool CanBuild(Entity unit, Entity target, in FactionRelations relations) =>
            _builders.HasComponent(unit) &&
            ConstructionRules.IsAlliedSite(_construction, _factions, relations, target, _factions[unit].Value);

        private bool CanRepair(Entity unit, Entity target, in FactionRelations relations) =>
            _builders.HasComponent(unit) &&
            RepairRules.NeedsRepair(_health, _construction, _factions, relations, target, _factions[unit].Value);

        private bool CanCapture(Entity unit, Entity target, in FactionRelations relations) =>
            _capturers.HasComponent(unit) && _capturables.HasComponent(target) && _targets.IsAlive(target) &&
            !relations.IsAllied(_factions[unit].Value, _factions[target].Value);

        private bool CanEnter(Entity unit, Entity target, in FactionRelations relations) =>
            unit != target && _passengers.TryGetComponent(unit, out var passenger) &&
            _containers.TryGetComponent(target, out var container) && container.Fits(passenger.Size) &&
            _targets.IsAlive(target) && !ConstructionRules.IsUnderConstruction(_construction, target) &&
            relations.IsAllied(_factions[unit].Value, _factions[target].Value);
    }
}
