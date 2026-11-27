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
using Unity.Mathematics;

namespace HyperRTS.Simulation.Commands;

/// <summary>
/// Picks the order a unit gets for a command from its capabilities and the target: the first of the command's
/// <see cref="CommandOrders"/> it can take, else their fallback. Smart tries the unit's
/// <see cref="SmartOrderRule"/>s first.
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
    private ComponentLookup<Flight> _flights;
    private BufferLookup<LandingPad> _pads;
    private CaptureLookup _capture;
    private Boarding _boarding;
    private BufferLookup<SmartOrderRule> _rules;
    private EntityManager _entityManager;

    public OrderResolver(ref SystemState state)
    {
        _owners = state.GetComponentLookup<Owner>(true);
        _targets = new TargetLookup(ref state);
        TargetLookup.Require(ref state);
        _mounts = state.GetBufferLookup<WeaponMount>(true);
        _harvesters = state.GetComponentLookup<Harvester>(true);
        _nodes = state.GetComponentLookup<ResourceNode>(true);
        _builders = state.GetComponentLookup<Builder>(true);
        _construction = state.GetComponentLookup<ConstructionProgress>(true);
        _repairs = new RepairLookup(ref state);
        _capturers = state.GetComponentLookup<Capturer>(true);
        _units = state.GetComponentLookup<UnitTag>(true);
        _padUsers = state.GetComponentLookup<HomePad>(true);
        _flights = state.GetComponentLookup<Flight>(true);
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
        _flights.Update(ref state);
        _pads.Update(ref state);
        _capture.Update(ref state);
        _boarding.Update(ref state);
        _rules.Update(ref state);
        _entityManager = state.EntityManager;
    }

    /// <summary>
    /// The order the unit gets, aimed where the command points. A unit whose weapon fires only on order takes an
    /// attack as a fire mission at the target's position.
    /// </summary>
    public Order Resolve(in PlayerCommand command, Entity unit, in OwnerRelations relations,
        in OrderTraitTable traits)
    {
        var order = new Order
        {
            Type = ResolveType(command, unit, relations, traits),
            Position = command.Position,
            Target = command.Target,
            Argument = command.Argument,
        };
        order.Facing = TurnsToFacing(order.Type, unit) ? command.Facing : float2.zero;
        if (order.Type == OrderType.FireMission && order.Target != Entity.Null)
        {
            order.Position = _targets.Position(order.Target);
            order.Target = Entity.Null;
        }

        return order;
    }

    /// <summary>A ground move ends turned to the command's facing, by any unit but one that can't stand in the air.</summary>
    private bool TurnsToFacing(OrderType type, Entity unit)
    {
        if (type is not (OrderType.Move or OrderType.AttackMove))
        {
            return false;
        }

        return !_flights.TryGetComponent(unit, out var flight) || flight.Hovers;
    }

    /// <summary>The order type a right-click on <paramref name="target"/> gives the unit.</summary>
    public OrderType ResolveSmart(Entity unit, Entity target, in OwnerRelations relations)
    {
        var type = TryRule(unit, target, relations, out var ruled)
            ? ruled
            : Pick(CommandOrders.For(CommandType.Smart), unit, target, relations, false);
        return AsFireMission(unit, type);
    }

    private OrderType ResolveType(in PlayerCommand command, Entity unit, in OwnerRelations relations,
        in OrderTraitTable traits)
    {
        if (command.Type == CommandType.Smart)
        {
            return ResolveSmart(unit, command.Target, relations);
        }

        var orders = command.Type >= CommandType.GameFirst
            ? CommandOrders.ForGame(command.Type, traits)
            : CommandOrders.For(command.Type);
        return AsFireMission(unit, Pick(orders, unit, command.Target, relations, true));
    }

    private OrderType AsFireMission(Entity unit, OrderType type) =>
        type == OrderType.Attack && FiresOnOrder(unit) ? OrderType.FireMission : type;

    private OrderType Pick(in CommandOrders orders, Entity unit, Entity target, in OwnerRelations relations,
        bool forced)
    {
        foreach (var order in orders.Choices)
        {
            if (CanTake(order, unit, target, relations, forced))
            {
                return order;
            }
        }

        return orders.Fallback;
    }

    /// <summary>
    /// Whether the unit can carry out <paramref name="order"/> on the target. A <paramref name="forced"/> command
    /// names its order, so an attack may hit scenery and a return to base needs no airfield to click on.
    /// </summary>
    private bool CanTake(OrderType order, Entity unit, Entity target, in OwnerRelations relations, bool forced)
    {
        return order switch
        {
            OrderType.Move or OrderType.ReverseMove or OrderType.Patrol => true,
            OrderType.AttackMove => TryGetPrimary(unit, out _),
            OrderType.Attack => forced ? CanForceAttack(unit, target, relations) : CanAttack(unit, target, relations),
            OrderType.FireMission => FiresOnOrder(unit),
            OrderType.Escort => CanEscort(unit, target, relations),
            OrderType.ReturnToBase => forced ? _padUsers.HasComponent(unit) : CanLandAt(unit, target),
            OrderType.Gather => _harvesters.HasComponent(unit) && _nodes.HasComponent(target),
            OrderType.Build => CanBuild(unit, target, relations),
            OrderType.Repair => CanRepair(unit, target, relations),
            OrderType.Capture => CanCapture(unit, target, relations),
            OrderType.Enter => _boarding.CanBoard(unit, target, relations),
            // A game order's own system checks its target.
            _ => order >= OrderType.GameFirst,
        };
    }

    private bool FiresOnOrder(Entity unit) =>
        _mounts.TryGetBuffer(unit, out var mounts) && new WeaponMounts(mounts).FiresOnOrder;

    private bool TryRule(Entity unit, Entity target, in OwnerRelations relations, out OrderType order)
    {
        order = OrderType.None;
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
                order = rule.Order;
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
        return _mounts.TryGetBuffer(unit, out var mounts) && new WeaponMounts(mounts).TryGetPrimary(out weapon);
    }

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
}
