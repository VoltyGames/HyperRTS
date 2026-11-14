using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources.Gathering;
using HyperRTS.Simulation.Transport;
using Unity.Entities;

namespace HyperRTS.Simulation.Commands
{
    /// <summary>
    /// Picks the order a unit gets for a command from its capabilities and the target: Smart tries the unit's
    /// <see cref="SmartOrderRule"/>s, then attack, gather, build, repair, enter, capture, land or move; explicit
    /// commands fall back to Move when the unit can't comply.
    /// </summary>
    public struct OrderResolver
    {
        private ComponentLookup<Owner> _owners;
        private TargetLookup _targets;
        private BufferLookup<WeaponMount> _mounts;
        private ComponentLookup<Harvester> _harvesters;
        private ComponentLookup<ResourceNode> _nodes;
        private ComponentLookup<Builder> _builders;
        private ComponentLookup<ConstructionProgress> _construction;
        private RepairLookup _repairs;
        private ComponentLookup<Capturer> _capturers;
        private ComponentLookup<UnitTag> _units;
        private ComponentLookup<HomePad> _padUsers;
        private BufferLookup<LandingPad> _pads;
        private CaptureLookup _capture;
        private Boarding _boarding;
        private BufferLookup<SmartOrderRule> _rules;
        private EntityManager _entityManager;

        public OrderResolver(ref SystemState state)
        {
            _owners = state.GetComponentLookup<Owner>(true);
            _targets = new TargetLookup(ref state);
            _mounts = state.GetBufferLookup<WeaponMount>(true);
            _harvesters = state.GetComponentLookup<Harvester>(true);
            _nodes = state.GetComponentLookup<ResourceNode>(true);
            _builders = state.GetComponentLookup<Builder>(true);
            _construction = state.GetComponentLookup<ConstructionProgress>(true);
            _repairs = new RepairLookup(ref state);
            _capturers = state.GetComponentLookup<Capturer>(true);
            _units = state.GetComponentLookup<UnitTag>(true);
            _padUsers = state.GetComponentLookup<HomePad>(true);
            _pads = state.GetBufferLookup<LandingPad>(true);
            _capture = new CaptureLookup(ref state);
            _boarding = new Boarding(ref state, true);
            _rules = state.GetBufferLookup<SmartOrderRule>(true);
            _entityManager = state.EntityManager;
        }

        /// <summary><paramref name="queries"/> are the system's, created once in OnCreate.</summary>
        public void Update(ref SystemState state, in TargetQueries queries)
        {
            _owners.Update(ref state);
            _targets.Update(ref state, queries);
            _mounts.Update(ref state);
            _harvesters.Update(ref state);
            _nodes.Update(ref state);
            _builders.Update(ref state);
            _construction.Update(ref state);
            _repairs.Update(ref state);
            _capturers.Update(ref state);
            _units.Update(ref state);
            _padUsers.Update(ref state);
            _pads.Update(ref state);
            _capture.Update(ref state);
            _boarding.Update(ref state);
            _rules.Update(ref state);
            _entityManager = state.EntityManager;
        }

        /// <summary>The order type the unit gets; callers fill in where and what it acts on.</summary>
        public Order Resolve(CommandType command, Entity unit, Entity target, in OwnerRelations relations)
        {
            return command == CommandType.Smart && TryRule(unit, target, relations, out var ruled)
                ? ruled
                : new Order { Type = ResolveType(command, unit, target, relations) };
        }

        private OrderType ResolveType(CommandType command, Entity unit, Entity target, in OwnerRelations relations)
        {
            return command switch
            {
                CommandType.Smart => ResolveSmart(unit, target, relations),
                CommandType.Attack => CanForceAttack(unit, target, relations) ? OrderType.Attack : OrderType.Move,
                CommandType.AttackMove => ResolveAttackMove(unit, target, relations),
                CommandType.Patrol => OrderType.Patrol,
                CommandType.ReverseMove => OrderType.ReverseMove,
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

        private OrderType ResolveAttackMove(Entity unit, Entity target, in OwnerRelations relations)
        {
            // Attack-move clicked on an enemy or on scenery attacks it directly.
            if (CanForceAttack(unit, target, relations))
            {
                return OrderType.Attack;
            }

            return TryGetPrimary(unit, out _) ? OrderType.AttackMove : OrderType.Move;
        }

        private OrderType ResolveSmart(Entity unit, Entity target, in OwnerRelations relations)
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

        private bool TryRule(Entity unit, Entity target, in OwnerRelations relations, out Order order)
        {
            order = default;
            if (target == Entity.Null || !_rules.TryGetBuffer(unit, out var rules))
            {
                return false;
            }

            var targetOwner = _owners.TryGetComponent(target, out var owner) ? owner.Value : Owner.Neutral;
            var relation = relations.GetRelation(_owners[unit].Value, targetOwner);
            foreach (var rule in rules)
            {
                if (!rule.Relations.Includes(relation))
                {
                    continue;
                }

                var type = TypeManager.GetTypeIndexFromStableTypeHash(rule.TargetComponent);
                if (_entityManager.HasComponent(target, ComponentType.FromTypeIndex(type)))
                {
                    order = new Order { Type = rule.Order };
                    return true;
                }
            }

            return false;
        }

        private bool CanEscort(Entity unit, Entity target, in OwnerRelations relations)
        {
            if (target == unit || !_units.HasComponent(target) || !_targets.IsAlive(target))
            {
                return false;
            }

            return relations.IsAllied(_owners[unit].Value, _owners[target].Value);
        }

        private bool CanAttack(Entity unit, Entity target, in OwnerRelations relations) =>
            TryGetPrimary(unit, out var weapon) &&
            _targets.IsValidTarget(target, _owners[unit].Value, relations, weapon.Targets);

        /// <summary>Explicit attack orders may also target destructible scenery; smart clicks never do.</summary>
        private bool CanForceAttack(Entity unit, Entity target, in OwnerRelations relations) =>
            TryGetPrimary(unit, out var weapon) &&
            _targets.IsAttackable(target, _owners[unit].Value, relations, weapon.Targets);

        private bool TryGetPrimary(Entity unit, out Weapon weapon)
        {
            weapon = default;
            return _mounts.TryGetBuffer(unit, out var mounts) && WeaponMount.TryGetPrimary(mounts, out weapon);
        }

        private bool CanGather(Entity unit, Entity target) =>
            _harvesters.HasComponent(unit) && _nodes.HasComponent(target);

        private bool CanBuild(Entity unit, Entity target, in OwnerRelations relations) =>
            _builders.HasComponent(unit) &&
            BuildingRules.IsAlliedSite(_construction, _owners, relations, target, _owners[unit].Value);

        private bool CanRepair(Entity unit, Entity target, in OwnerRelations relations) =>
            _builders.HasComponent(unit) &&
            _repairs.NeedsRepair(target, _owners[unit].Value, relations);

        private bool CanCapture(Entity unit, Entity target, in OwnerRelations relations) =>
            _capturers.HasComponent(unit) && _capture.CanCapture(target, _owners[unit].Value, relations);

        private bool CanLandAt(Entity unit, Entity target) =>
            _padUsers.HasComponent(unit) &&
            AirfieldRules.IsOwnedAirfield(_pads, _owners, _construction, target, _owners[unit].Value);

        private bool CanEnter(Entity unit, Entity target, in OwnerRelations relations) =>
            _boarding.CanBoard(unit, target, relations);
    }
}
