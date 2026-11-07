using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Handles the Retreat command: each squad gets a <see cref="OrderType.Retreat"/> order to the nearest friendly
    /// working <see cref="RetreatPoint"/>, moving on to another point if its own is lost.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(MoveOrderSystem))]
    public partial struct RetreatSystem : ISystem
    {
        private CommandReader _commands;
        private OrderWriter _writer;
        private EntityQuery _points;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<SquadMember, Selected, Owner>().WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, TypeSet.Single(CommandType.Retreat), selected);
            _writer = new OrderWriter(ref state);
            _points = SystemAPI.QueryBuilder().WithAll<RetreatPoint, Owner, LocalTransform>()
                .WithNone<Dead, Offline>().Build();
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
                    if (SystemAPI.HasBuffer<SquadMember>(squad) && _writer.CanReceiveOrders(squad))
                    {
                        Retreat(ref state, squad, relations, points);
                    }
                }
            }

            var running = new NativeList<Entity>(Allocator.Temp);
            foreach (var (order, retreating, entity) in SystemAPI
                         .Query<RefRO<ActiveOrder>, EnabledRefRW<Retreating>>().WithAll<SquadMember>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                var ordered = SystemAPI.IsComponentEnabled<ActiveOrder>(entity);
                var isRetreat = ordered && order.ValueRO.Value.Type == OrderType.Retreat;
                retreating.ValueRW = isRetreat;
                if (isRetreat)
                {
                    running.Add(entity);
                }
            }

            foreach (var squad in running)
            {
                Continue(ref state, squad, relations, points);
            }
        }

        /// <summary>Sends the squad to its nearest retreat point; false when there is none.</summary>
        private bool Retreat(ref SystemState state, Entity squad, in OwnerRelations relations, in SquadPoints points)
        {
            var owner = SystemAPI.GetComponent<Owner>(squad).Value;
            var here = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            var point = points.NearestAllied(here, owner, relations);
            if (point == Entity.Null)
            {
                return false;
            }

            var position = SystemAPI.GetComponent<LocalTransform>(point).Position;
            _writer.Issue(squad, new Order { Type = OrderType.Retreat, Position = position, Target = point }, false);
            return true;
        }

        /// <summary>Ends the retreat on arrival; a lost or offline point sends the squad to the next one.</summary>
        private void Continue(ref SystemState state, Entity squad, in OwnerRelations relations, in SquadPoints points)
        {
            var target = SystemAPI.GetComponent<ActiveOrder>(squad).Value.Target;
            if (points.Contains(target))
            {
                if (points.Reaches(target, SystemAPI.GetComponent<LocalTransform>(squad).Position))
                {
                    _writer.Stop(squad);
                }

                return;
            }

            if (!Retreat(ref state, squad, relations, points))
            {
                _writer.Stop(squad);
            }
        }
    }
}
