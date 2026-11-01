using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Reach;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>
    /// Runs Enter orders: passengers walk to an allied container and board once in reach and if they fit. Aboard,
    /// they drop orders, target and selection, and hold position (firing out) or go passive.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct BoardingSystem : ISystem
    {
        private Boarding _boarding;
        private TargetReach _reach;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _boarding = new Boarding(ref state, false);
            _reach = new TargetReach(ref state);
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _boarding.Update(ref state);
            _reach.Update(ref state, SystemAPI.GetSingleton<MapSettings>());

            new BoardJob
            {
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Boarding = _boarding,
                ContainerLookup = SystemAPI.GetComponentLookup<Container>(true),
                Reach = _reach,
                StanceLookup = SystemAPI.GetComponentLookup<CombatStance>(false),
                AttackLookup = SystemAPI.GetComponentLookup<AttackTarget>(false),
                SelectedLookup = SystemAPI.GetComponentLookup<Selected>(false),
                QueueLookup = SystemAPI.GetBufferLookup<QueuedOrder>(false),
            }.Schedule();
        }

        /// <summary>Single-threaded: several passengers may board one container in a frame.</summary>
        [BurstCompile]
        [WithAll(typeof(Passenger))]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination), typeof(Arrival))]
        private partial struct BoardJob : IJobEntity
        {
            public FactionRelations Relations;
            public Boarding Boarding;
            [ReadOnly] public ComponentLookup<Container> ContainerLookup;
            public TargetReach Reach;
            public ComponentLookup<CombatStance> StanceLookup;
            public ComponentLookup<AttackTarget> AttackLookup;
            public ComponentLookup<Selected> SelectedLookup;
            public BufferLookup<QueuedOrder> QueueLookup;

            private void Execute(Entity entity, ref ActiveOrder order,
                EnabledRefRW<ActiveOrder> busy, RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> moving,
                EnabledRefRW<Arrival> arrived, in NavAgent agent)
            {
                var entering = busy.ValueRO && order.Value.Type == OrderType.Enter;
                if (!entering || Boarding.IsInside(entity))
                {
                    return;
                }

                var container = order.Value.Target;
                if (!Boarding.CanBoard(entity, container, Relations))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                var walk = Reach.Approach(container, Reach.Transform(entity), agent,
                    new MoveControl(destination, moving, arrived));
                if (ActiveOrder.StopsShort(walk, busy, moving))
                {
                    return;
                }

                ActiveOrder.Finish(busy, moving);

                Boarding.Board(entity, container, Settle(entity, container));
                QueueLookup[entity].Clear();
            }

            /// <summary>Drops target and selection and picks the inside stance; returns the stance to restore.</summary>
            private Stance Settle(Entity entity, Entity container)
            {
                AttackLookup.DisableIfPresent(entity);
                SelectedLookup.DisableIfPresent(entity);

                if (!StanceLookup.TryGetComponent(entity, out var stance))
                {
                    return Stance.Aggressive;
                }

                var inside = ContainerLookup[container].PassengersFire ? Stance.HoldPosition : Stance.Passive;
                StanceLookup[entity] = new CombatStance { Value = inside, ReturnPoint = stance.ReturnPoint };
                return stance.Value;
            }
        }
    }
}
