using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Handles the Retreat command for the commanded or selected squads: each gets a <see cref="OrderType.Retreat"/>
    /// order to the nearest friendly working <see cref="RetreatPoint"/>, which the move order walks and
    /// <see cref="SquadPostureSystem"/> speeds up. The order ends once the squad is within the point's radius, or
    /// moves on to another point when its own is lost, or stops if none is left.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(MoveOrderSystem))]
    public partial struct RetreatSystem : ISystem
    {
        private CommandReader _commands;
        private OrderWriter _writer;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<SquadMember, Selected, Faction>().WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, TypeSet.Single(CommandType.Retreat), selected);
            _writer = new OrderWriter(ref state);
            state.RequireForUpdate<SquadMember>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _writer.Update(ref state, SystemAPI.GetSingleton<OrderTraitTable>());
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            foreach (var each in _commands.Read(ref state))
            {
                foreach (var squad in _commands.Subjects(ref state, each))
                {
                    if (SystemAPI.HasBuffer<SquadMember>(squad) && _writer.CanReceiveOrders(squad))
                    {
                        Retreat(ref state, squad, relations);
                    }
                }
            }

            var retreating = new NativeList<Entity>(Allocator.Temp);
            foreach (var (order, entity) in SystemAPI.Query<RefRO<ActiveOrder>>().WithAll<SquadMember>().WithEntityAccess())
            {
                if (order.ValueRO.Value.Type == OrderType.Retreat)
                {
                    retreating.Add(entity);
                }
            }

            foreach (var squad in retreating)
            {
                Continue(ref state, squad, relations);
            }
        }

        /// <summary>Sends the squad to its nearest retreat point; false when there is none.</summary>
        private bool Retreat(ref SystemState state, Entity squad, in FactionRelations relations)
        {
            if (!TryFindPoint(ref state, squad, relations, out var point))
            {
                return false;
            }

            var position = SystemAPI.GetComponent<LocalTransform>(point).Position;
            _writer.Issue(squad, new Order { Type = OrderType.Retreat, Position = position, Target = point }, false);
            return true;
        }

        /// <summary>Ends the retreat on arrival; a lost point sends the squad to the next one.</summary>
        private void Continue(ref SystemState state, Entity squad, in FactionRelations relations)
        {
            var target = SystemAPI.GetComponent<ActiveOrder>(squad).Value.Target;
            var here = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            // Dead is on every entity, enabled only the frame it dies.
            var dead = SystemAPI.HasComponent<Dead>(target) && SystemAPI.IsComponentEnabled<Dead>(target);
            if (SystemAPI.HasComponent<RetreatPoint>(target) && SystemAPI.HasComponent<LocalTransform>(target) && !dead)
            {
                var radius = SystemAPI.GetComponent<RetreatPoint>(target).Radius;
                var there = SystemAPI.GetComponent<LocalTransform>(target).Position;
                if (math.distancesq(here.xz, there.xz) <= radius * radius)
                {
                    _writer.Stop(squad);
                }

                return;
            }

            if (!Retreat(ref state, squad, relations))
            {
                _writer.Stop(squad);
            }
        }

        private bool TryFindPoint(ref SystemState state, Entity squad, in FactionRelations relations, out Entity found)
        {
            var faction = SystemAPI.GetComponent<Faction>(squad).Value;
            var here = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            found = Entity.Null;
            var best = float.MaxValue;
            foreach (var (owner, transform, entity) in SystemAPI.Query<RefRO<Faction>, RefRO<LocalTransform>>()
                         .WithAll<RetreatPoint>().WithNone<Dead, Offline>().WithEntityAccess())
            {
                var distance = math.distancesq(here.xz, transform.ValueRO.Position.xz);
                if (distance < best && relations.IsAllied(faction, owner.ValueRO.Value))
                {
                    best = distance;
                    found = entity;
                }
            }

            return found != Entity.Null;
        }
    }
}
