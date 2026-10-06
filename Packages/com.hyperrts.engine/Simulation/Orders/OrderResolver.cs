using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Units;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Picks the order a unit gets for a command from its capabilities and the target: Smart chooses
    /// attack/gather/build/repair/enter/capture/land/move, explicit commands fall back to Move when the unit can't
    /// comply (Return to Base is skipped by units without a pad).
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
        private ComponentLookup<UnitTag> _units;
        private ComponentLookup<PadHome> _padUsers;
        private BufferLookup<LandingPad> _pads;
        private CaptureRules _capture;
        private Boarding _boarding;

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
            _units = state.GetComponentLookup<UnitTag>(true);
            _padUsers = state.GetComponentLookup<PadHome>(true);
            _pads = state.GetBufferLookup<LandingPad>(true);
            _capture = new CaptureRules(ref state);
            _boarding = new Boarding(ref state, true);
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
            _units.Update(ref state);
            _padUsers.Update(ref state);
            _pads.Update(ref state);
            _capture.Update(ref state);
            _boarding.Update(ref state);
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
                CommandType.Patrol => OrderType.Patrol,
                CommandType.Escort => CanEscort(unit, target, relations) ? OrderType.Escort : OrderType.Move,
                CommandType.ReturnToBase => _padUsers.HasComponent(unit) ? OrderType.ReturnToBase : OrderType.None,
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

            if (CanLandAt(unit, target))
            {
                return OrderType.ReturnToBase;
            }

            return CanCapture(unit, target, relations) ? OrderType.Capture : OrderType.Move;
        }

        private bool CanEscort(Entity unit, Entity target, in FactionRelations relations)
        {
            if (target == unit || !_units.HasComponent(target) || !_targets.IsAlive(target))
            {
                return false;
            }

            return relations.IsAllied(_factions[unit].Value, _factions[target].Value);
        }

        private bool CanAttack(Entity unit, Entity target, in FactionRelations relations) =>
            _weapons.TryGetComponent(unit, out var weapon) &&
            _targets.IsValidTarget(target, _factions[unit].Value, relations, weapon.Targets);

        private bool CanGather(Entity unit, Entity target) =>
            _harvesters.HasComponent(unit) && _nodes.HasComponent(target);

        private bool CanBuild(Entity unit, Entity target, in FactionRelations relations) =>
            _builders.HasComponent(unit) &&
            ConstructionRules.IsAlliedSite(_construction, _factions, relations, target, _factions[unit].Value);

        private bool CanRepair(Entity unit, Entity target, in FactionRelations relations) =>
            _builders.HasComponent(unit) &&
            RepairRules.NeedsRepair(_health, _construction, _factions, relations, target, _factions[unit].Value);

        private bool CanCapture(Entity unit, Entity target, in FactionRelations relations) =>
            _capturers.HasComponent(unit) && _capture.CanCapture(target, _factions[unit].Value, relations);

        private bool CanLandAt(Entity unit, Entity target) =>
            _padUsers.HasComponent(unit) &&
            AirfieldRules.IsAirfieldOf(_pads, _factions, _construction, target, _factions[unit].Value);

        private bool CanEnter(Entity unit, Entity target, in FactionRelations relations) =>
            _boarding.CanBoard(unit, target, relations);
    }
}
