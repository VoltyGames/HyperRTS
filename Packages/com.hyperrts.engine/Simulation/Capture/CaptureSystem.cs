using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Units;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>
    /// Runs Capture orders: capturers walk to the building and add <c>Rate / CaptureTime</c> progress per second.
    /// A different player starting over resets it. On completion the building changes owner at the end of the frame,
    /// refunds its queue to the old owner, drops its target and selection, and a single-use capturer is consumed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct CaptureSystem : ISystem
    {
        private CaptureRules _rules;
        private ComponentLookup<Capturable> _capturableLookup;
        private ComponentLookup<CaptureProgress> _progressLookup;
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<NavObstacle> _obstacleLookup;
        private ComponentLookup<AttackTarget> _attackLookup;
        private BufferLookup<ProductionQueueItem> _queueLookup;
        private BufferLookup<ResourceStock> _stockLookup;
        private BufferLookup<ResourceCost> _costLookup;
        private ComponentLookup<Selected> _selectedLookup;
        private EntityQuery _players;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _rules = new CaptureRules(ref state);
            _capturableLookup = state.GetComponentLookup<Capturable>(true);
            _progressLookup = state.GetComponentLookup<CaptureProgress>();
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _obstacleLookup = state.GetComponentLookup<NavObstacle>(true);
            _attackLookup = state.GetComponentLookup<AttackTarget>(true);
            _queueLookup = state.GetBufferLookup<ProductionQueueItem>(true);
            _stockLookup = state.GetBufferLookup<ResourceStock>();
            _costLookup = state.GetBufferLookup<ResourceCost>(true);
            _selectedLookup = state.GetComponentLookup<Selected>(true);
            _players = SystemAPI.QueryBuilder().WithAll<Player>().Build();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _rules.Update(ref state);
            _capturableLookup.Update(ref state);
            _progressLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _obstacleLookup.Update(ref state);
            _attackLookup.Update(ref state);
            _queueLookup.Update(ref state);
            _stockLookup.Update(ref state);
            _costLookup.Update(ref state);
            _selectedLookup.Update(ref state);

            new CaptureJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Rules = _rules,
                CapturableLookup = _capturableLookup,
                ProgressLookup = _progressLookup,
                FactionLookup = _factionLookup,
                TransformLookup = _transformLookup,
                ObstacleLookup = _obstacleLookup,
                AttackLookup = _attackLookup,
                QueueLookup = _queueLookup,
                StockLookup = _stockLookup,
                CostLookup = _costLookup,
                SelectedLookup = _selectedLookup,
                PlayerByFaction = PlayerLookup.ByFaction(_players, state.WorldUpdateAllocator),
            }.Schedule();
        }

        /// <summary>Single-threaded so several capturers can work on one building.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination))]
        private partial struct CaptureJob : IJobEntity
        {
            public float DeltaTime;
            public FactionRelations Relations;
            public EntityCommandBuffer Ecb;
            public CaptureRules Rules;
            [ReadOnly] public ComponentLookup<Capturable> CapturableLookup;
            public ComponentLookup<CaptureProgress> ProgressLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            [ReadOnly] public ComponentLookup<AttackTarget> AttackLookup;
            [ReadOnly] public BufferLookup<ProductionQueueItem> QueueLookup;
            public BufferLookup<ResourceStock> StockLookup;
            [ReadOnly] public BufferLookup<ResourceCost> CostLookup;
            [ReadOnly] public ComponentLookup<Selected> SelectedLookup;
            [ReadOnly] public NativeArray<Entity> PlayerByFaction;

            private void Execute(Entity entity, in Capturer capturer, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, in LocalTransform transform,
                in NavAgent agent)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.Capture)
                {
                    return;
                }

                var target = order.Value.Target;
                var faction = FactionLookup[entity].Value;
                if (!Rules.CanCapture(target, faction, Relations))
                {
                    busy.ValueRW = false;
                    moving.ValueRW = false;
                    return;
                }

                var position = TransformLookup[target].Position;
                if (!ReachMath.InReach(transform.Position, agent.Radius, position, ReachMath.HalfExtents(ObstacleLookup, target)))
                {
                    ReachMath.MoveTo(ref destination, moving, position);
                    return;
                }

                moving.ValueRW = false;
                if (Advance(target, faction, capturer.Rate))
                {
                    TakeOver(target, faction);
                    busy.ValueRW = false;
                    if (capturer.ConsumedOnCapture)
                    {
                        Ecb.DestroyEntity(entity);
                    }
                }
            }

            /// <summary>Adds this frame's work and returns true once the capture completes.</summary>
            private bool Advance(Entity target, byte faction, float rate)
            {
                ref var progress = ref ProgressLookup.GetRefRW(target).ValueRW;
                if (progress.Faction != faction)
                {
                    progress = new CaptureProgress { Faction = faction };
                }

                progress.Value += DeltaTime * rate / CapturableLookup[target].CaptureTime;
                if (progress.Value < 1f)
                {
                    return false;
                }

                progress = default;
                return true;
            }

            private void TakeOver(Entity building, byte faction)
            {
                var previous = FactionLookup[building].Value;
                Ecb.SetComponent(building, new Faction { Value = faction });
                if (QueueLookup.TryGetBuffer(building, out var queue))
                {
                    Refund(queue, previous);
                    Ecb.SetBuffer<ProductionQueueItem>(building);
                }

                if (AttackLookup.HasComponent(building))
                {
                    Ecb.SetComponentEnabled<AttackTarget>(building, false);
                }

                if (SelectedLookup.HasComponent(building))
                {
                    Ecb.SetComponentEnabled<Selected>(building, false);
                }
            }

            /// <summary>Lost production is refunded to whoever paid, as a cancel would.</summary>
            private void Refund(DynamicBuffer<ProductionQueueItem> queue, byte owner)
            {
                if (!StockLookup.TryGetBuffer(PlayerByFaction[owner], out var stock))
                {
                    return;
                }

                foreach (var item in queue)
                {
                    if (CostLookup.TryGetBuffer(item.Prefab, out var cost))
                    {
                        ResourceMath.Refund(stock, cost);
                    }
                }
            }
        }
    }
}
