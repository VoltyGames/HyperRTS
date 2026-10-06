using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Units;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

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
        private ComponentLookup<Container> _containerLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<NavObstacle> _obstacleLookup;
        private ComponentLookup<CombatStance> _stanceLookup;
        private ComponentLookup<AttackTarget> _attackLookup;
        private ComponentLookup<Selected> _selectedLookup;
        private BufferLookup<QueuedOrder> _queueLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _boarding = new Boarding(ref state, false);
            _containerLookup = state.GetComponentLookup<Container>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _obstacleLookup = state.GetComponentLookup<NavObstacle>(true);
            _stanceLookup = state.GetComponentLookup<CombatStance>();
            _attackLookup = state.GetComponentLookup<AttackTarget>();
            _selectedLookup = state.GetComponentLookup<Selected>();
            _queueLookup = state.GetBufferLookup<QueuedOrder>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _boarding.Update(ref state);
            _containerLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _obstacleLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _attackLookup.Update(ref state);
            _selectedLookup.Update(ref state);
            _queueLookup.Update(ref state);

            new BoardJob
            {
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Boarding = _boarding,
                ContainerLookup = _containerLookup,
                TransformLookup = _transformLookup,
                ObstacleLookup = _obstacleLookup,
                StanceLookup = _stanceLookup,
                AttackLookup = _attackLookup,
                SelectedLookup = _selectedLookup,
                QueueLookup = _queueLookup,
            }.Schedule();
        }

        /// <summary>Single-threaded: several passengers may board one container in a frame.</summary>
        [BurstCompile]
        [WithAll(typeof(Passenger))]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination), typeof(Inside))]
        private partial struct BoardJob : IJobEntity
        {
            public FactionRelations Relations;
            public Boarding Boarding;
            [ReadOnly] public ComponentLookup<Container> ContainerLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            public ComponentLookup<CombatStance> StanceLookup;
            public ComponentLookup<AttackTarget> AttackLookup;
            public ComponentLookup<Selected> SelectedLookup;
            public BufferLookup<QueuedOrder> QueueLookup;

            private void Execute(Entity entity, ref ActiveOrder order,
                EnabledRefRW<ActiveOrder> busy, ref MoveDestination destination, EnabledRefRW<MoveDestination> moving,
                in NavAgent agent, ref Inside inside, EnabledRefRW<Inside> aboard)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.Enter || aboard.ValueRO)
                {
                    return;
                }

                var container = order.Value.Target;
                if (!Boarding.CanBoard(entity, container, Relations))
                {
                    busy.ValueRW = false;
                    moving.ValueRW = false;
                    return;
                }

                var position = TransformLookup[container].Position;
                if (!ReachMath.InReach(TransformLookup[entity].Position, agent.Radius, position,
                        ReachMath.HalfExtents(ObstacleLookup, container)))
                {
                    ReachMath.MoveTo(ref destination, moving, position);
                    return;
                }

                busy.ValueRW = false;
                moving.ValueRW = false;
                aboard.ValueRW = true;
                inside = new Inside { Container = container, Stance = Settle(entity, container) };
                Boarding.Board(entity, container);
                QueueLookup[entity].Clear();
            }

            /// <summary>Drops target and selection and picks the inside stance; returns the stance to restore.</summary>
            private Stance Settle(Entity entity, Entity container)
            {
                if (AttackLookup.HasComponent(entity))
                {
                    AttackLookup.SetComponentEnabled(entity, false);
                }

                if (SelectedLookup.HasComponent(entity))
                {
                    SelectedLookup.SetComponentEnabled(entity, false);
                }

                if (!StanceLookup.TryGetComponent(entity, out var stance))
                {
                    return Stance.Aggressive;
                }

                var inside = ContainerLookup[container].PassengersFire ? Stance.HoldPosition : Stance.Passive;
                StanceLookup[entity] = new CombatStance { Value = inside, Anchor = stance.Anchor };
                return stance.Value;
            }
        }
    }
}
