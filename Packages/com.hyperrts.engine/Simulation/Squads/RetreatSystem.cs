using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Handles the Retreat command and owns the <see cref="OrderType.Retreat"/> order: each squad runs to the nearest
    /// friendly working <see cref="RetreatPoint"/>, moves on to another point if its own is lost, and finishes on
    /// reaching it, keeping any Shift-queued orders.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(MoveOrderSystem))]
    public partial struct RetreatSystem : ISystem
    {
        private CommandReader _commands;
        private OrderWriter _writer;
        private EntityQuery _points;
        private ComponentLookup<ActiveOrder> _orders;
        private ComponentLookup<MoveDestination> _moves;
        private ComponentLookup<Arrival> _arrivals;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<SquadMember, Selected, Owner>().Build();
            _commands = new CommandReader(ref state, TypeSet.Single(CommandType.Retreat), selected);
            _writer = new OrderWriter(ref state);
            _points = SystemAPI.QueryBuilder().WithAll<RetreatPoint, Owner, LocalTransform>()
                .WithNone<Offline>().Build();
            _orders = state.GetComponentLookup<ActiveOrder>();
            _moves = state.GetComponentLookup<MoveDestination>();
            _arrivals = state.GetComponentLookup<Arrival>();
            state.RequireForUpdate<SquadMember>();
            state.RequireForUpdate<OwnerRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _writer.Update(ref state, SystemAPI.GetSingleton<OrderTraitTable>());
            var relations = SystemAPI.GetSingleton<OwnerRelations>();
            var points = SquadPoints.From<RetreatPoint>(_points);
            foreach (var each in _commands.Read(ref state))
            {
                foreach (var squad in _commands.Subjects(ref state, each))
                {
                    if (SystemAPI.HasBuffer<SquadMember>(squad) && _writer.Reader.CanReceiveOrders(squad))
                    {
                        Retreat(ref state, squad, relations, points);
                    }
                }
            }

            _orders.Update(ref state);
            _moves.Update(ref state);
            _arrivals.Update(ref state);
            var running = new NativeList<Entity>(Allocator.Temp);
            foreach (var (retreating, entity) in SystemAPI.Query<EnabledRefRW<Retreating>>().WithAll<SquadMember>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                var isRetreat = ActiveOrder.Current(_orders, entity).Type == OrderType.Retreat;
                retreating.ValueRW = isRetreat;
                if (isRetreat)
                {
                    running.Add(entity);
                }
            }

            foreach (var squad in running)
            {
                SystemAPI.SetComponentEnabled<Retreating>(squad, Continue(ref state, squad, relations, points));
            }
        }

        /// <summary>Gives the squad a Retreat order to its nearest retreat point, if it has one.</summary>
        private void Retreat(ref SystemState state, Entity squad, in OwnerRelations relations, in SquadPoints points)
        {
            var point = NearestPoint(ref state, squad, relations, points);
            if (point != Entity.Null)
            {
                _writer.Issue(squad, RetreatTo(ref state, point), false);
            }
        }

        /// <summary>
        /// Walks to the point and finishes there, false once finished; a lost or offline point sends the squad to the
        /// next one.
        /// </summary>
        private bool Continue(ref SystemState state, Entity squad, in OwnerRelations relations, in SquadPoints points)
        {
            var busy = _orders.GetEnabledRefRW<ActiveOrder>(squad);
            var moving = _moves.GetEnabledRefRW<MoveDestination>(squad);
            var point = _orders[squad].Value.Target;
            if (!points.Contains(point))
            {
                point = NearestPoint(ref state, squad, relations, points);
                if (point == Entity.Null)
                {
                    ActiveOrder.Finish(busy, moving);
                    return false;
                }

                _orders.GetRefRW(squad).ValueRW.Value = RetreatTo(ref state, point);
                Walk(squad, moving);
                return true;
            }

            // Arrival also covers a walk locomotion gave up on, so a blocked squad doesn't retreat forever.
            var here = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            if (points.Reaches(point, here) || _arrivals.IsComponentEnabled(squad))
            {
                ActiveOrder.Finish(busy, moving);
                return false;
            }

            if (!moving.ValueRO)
            {
                Walk(squad, moving);
            }

            return true;
        }

        private void Walk(Entity squad, EnabledRefRW<MoveDestination> moving)
        {
            _moves[squad] = new MoveDestination { Value = _orders[squad].Value.Position };
            moving.ValueRW = true;
            _arrivals.SetComponentEnabled(squad, false);
        }

        private Entity NearestPoint(ref SystemState state, Entity squad, in OwnerRelations relations,
            in SquadPoints points)
        {
            var owner = SystemAPI.GetComponent<Owner>(squad).Value;
            var here = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            return points.NearestAllied(here, owner, relations);
        }

        private Order RetreatTo(ref SystemState state, Entity point) => new()
        {
            Type = OrderType.Retreat, Position = SystemAPI.GetComponent<LocalTransform>(point).Position, Target = point,
        };
    }
}
