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
        private ComponentLookup<Container> _containerLookup;
        private BufferLookup<Cargo> _cargoLookup;
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<ConstructionProgress> _siteLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<NavObstacle> _obstacleLookup;
        private ComponentLookup<CombatStance> _stanceLookup;
        private ComponentLookup<AttackTarget> _attackLookup;
        private ComponentLookup<Selected> _selectedLookup;
        private BufferLookup<QueuedOrder> _queueLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _containerLookup = state.GetComponentLookup<Container>();
            _cargoLookup = state.GetBufferLookup<Cargo>();
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _healthLookup = state.GetComponentLookup<Health>(true);
            _siteLookup = state.GetComponentLookup<ConstructionProgress>(true);
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
            _containerLookup.Update(ref state);
            _cargoLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _siteLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _obstacleLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _attackLookup.Update(ref state);
            _selectedLookup.Update(ref state);
            _queueLookup.Update(ref state);

            new BoardJob
            {
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                ContainerLookup = _containerLookup,
                CargoLookup = _cargoLookup,
                FactionLookup = _factionLookup,
                HealthLookup = _healthLookup,
                SiteLookup = _siteLookup,
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
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination), typeof(Inside))]
        private partial struct BoardJob : IJobEntity
        {
            public FactionRelations Relations;
            public ComponentLookup<Container> ContainerLookup;
            public BufferLookup<Cargo> CargoLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<ConstructionProgress> SiteLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            public ComponentLookup<CombatStance> StanceLookup;
            public ComponentLookup<AttackTarget> AttackLookup;
            public ComponentLookup<Selected> SelectedLookup;
            public BufferLookup<QueuedOrder> QueueLookup;

            private void Execute(Entity entity, in Passenger passenger, ref ActiveOrder order,
                EnabledRefRW<ActiveOrder> busy, ref MoveDestination destination, EnabledRefRW<MoveDestination> moving,
                in NavAgent agent, ref Inside inside, EnabledRefRW<Inside> aboard)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.Enter || aboard.ValueRO)
                {
                    return;
                }

                var container = order.Value.Target;
                if (!CanBoard(entity, container, passenger.Size))
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
                ContainerLookup.GetRefRW(container).ValueRW.Used += passenger.Size;
                CargoLookup[container].Add(new Cargo { Unit = entity });
                QueueLookup[entity].Clear();
            }

            private bool CanBoard(Entity entity, Entity container, int size) =>
                ContainerLookup.TryGetComponent(container, out var data) && data.Fits(size) &&
                HealthLookup.TryGetComponent(container, out var health) && health.Current > 0f &&
                !ConstructionRules.IsUnderConstruction(SiteLookup, container) &&
                Relations.IsAllied(FactionLookup[entity].Value, FactionLookup[container].Value);

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
