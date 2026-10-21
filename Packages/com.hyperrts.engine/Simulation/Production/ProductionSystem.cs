using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Trains the head of each completed producer's queue while the owner has population room, then spawns it at the
    /// spawn offset, raises <see cref="UnitProduced"/> and sends it to the rally point. Aircraft that use pads wait for
    /// a free pad at an airfield and spawn docked on it. Finished research is recorded on the owner instead.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    [UpdateAfter(typeof(PopulationSystem))]
    public partial struct ProductionSystem : ISystem
    {
        private ComponentLookup<Population> _populationLookup;
        private ComponentLookup<Producible> _producibleLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<PostTransformMatrix> _stretchLookup;
        private ComponentLookup<ActiveOrder> _orderLookup;
        private ComponentLookup<Research> _researchLookup;
        private ComponentLookup<Offline> _offlineLookup;
        private BufferLookup<CompletedResearch> _completedResearchLookup;
        private BufferLookup<LandingPad> _padLookup;
        private ComponentLookup<HomePad> _padUserLookup;
        private BufferLookup<UnitProduced> _producedLookup;
        private BufferLookup<ResearchCompleted> _researchedLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _populationLookup = state.GetComponentLookup<Population>(true);
            _producibleLookup = state.GetComponentLookup<Producible>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _stretchLookup = state.GetComponentLookup<PostTransformMatrix>(true);
            _orderLookup = state.GetComponentLookup<ActiveOrder>(true);
            _researchLookup = state.GetComponentLookup<Research>(true);
            _offlineLookup = state.GetComponentLookup<Offline>(true);
            _completedResearchLookup = state.GetBufferLookup<CompletedResearch>();
            _padLookup = state.GetBufferLookup<LandingPad>(true);
            _padUserLookup = state.GetComponentLookup<HomePad>(true);
            _producedLookup = state.GetBufferLookup<UnitProduced>();
            _researchedLookup = state.GetBufferLookup<ResearchCompleted>();
            state.RequireForUpdate<GameEvents>();
            state.RequireForUpdate<MatchRules>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _populationLookup.Update(ref state);
            _producibleLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _stretchLookup.Update(ref state);
            _orderLookup.Update(ref state);
            _researchLookup.Update(ref state);
            _offlineLookup.Update(ref state);
            _completedResearchLookup.Update(ref state);
            _padLookup.Update(ref state);
            _padUserLookup.Update(ref state);
            _producedLookup.Update(ref state);
            _researchedLookup.Update(ref state);

            var allocator = state.WorldUpdateAllocator;
            new ProduceJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                LowPowerRate = SystemAPI.GetSingleton<MatchRules>().LowPowerProductionRate,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                SpawnedPopulation = CollectionHelper.CreateNativeArray<int>(PlayerRegistry.Capacity, allocator),
                PopulationLookup = _populationLookup,
                ProducibleLookup = _producibleLookup,
                TransformLookup = _transformLookup,
                StretchLookup = _stretchLookup,
                OrderLookup = _orderLookup,
                ResearchLookup = _researchLookup,
                OfflineLookup = _offlineLookup,
                CompletedResearchLookup = _completedResearchLookup,
                PadLookup = _padLookup,
                PadUserLookup = _padUserLookup,
                Produced = _producedLookup,
                Researched = _researchedLookup,
                Events = SystemAPI.GetSingletonEntity<GameEvents>(),
            }.Schedule();
        }

        /// <summary>Single-threaded so units finished this frame count against the population cap right away.</summary>
        [BurstCompile]
        [WithNone(typeof(ConstructionProgress), typeof(Dead))]
        [WithPresent(typeof(RallyPoint))]
        private partial struct ProduceJob : IJobEntity
        {
            public float DeltaTime;
            public float LowPowerRate;
            public EntityCommandBuffer Ecb;
            public PlayerRegistry Players;
            public NativeArray<int> SpawnedPopulation;
            [ReadOnly] public ComponentLookup<Population> PopulationLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<PostTransformMatrix> StretchLookup;
            [ReadOnly] public ComponentLookup<ActiveOrder> OrderLookup;
            [ReadOnly] public ComponentLookup<Research> ResearchLookup;
            [ReadOnly] public ComponentLookup<Offline> OfflineLookup;
            public BufferLookup<CompletedResearch> CompletedResearchLookup;
            [ReadOnly] public BufferLookup<LandingPad> PadLookup;
            [ReadOnly] public ComponentLookup<HomePad> PadUserLookup;
            public BufferLookup<UnitProduced> Produced;
            public BufferLookup<ResearchCompleted> Researched;
            public Entity Events;

            private void Execute(Entity entity, ref Producer producer, DynamicBuffer<ProductionQueueItem> queue,
                in LocalTransform transform, in Faction faction, in RallyPoint rally, EnabledRefRO<RallyPoint> hasRally)
            {
                if (queue.IsEmpty)
                {
                    producer.Elapsed = 0f;
                    return;
                }

                var prefab = queue[0].Prefab;
                var producible = ProducibleLookup.TryGetComponent(prefab, out var data) ? data : default;
                if (!HasRoomFor(faction.Value, producible.Population) || !TryReservePad(entity, prefab, out var pad))
                {
                    return;
                }

                // Low power only slows production; any other reason (an EMP) halts it.
                var offline = OfflineLookup.TryGetComponent(entity, out var found) ? found : default;
                if (offline.HasReasonOtherThan(OfflineReason.Unpowered))
                {
                    return;
                }

                var lowPower = (offline.Reasons & OfflineReason.Unpowered) != 0;
                producer.Elapsed += DeltaTime * producer.Speed * (lowPower ? LowPowerRate : 1f);
                if (producer.Elapsed < producible.BuildTime)
                {
                    return;
                }

                if (ResearchLookup.HasComponent(prefab))
                {
                    RecordResearch(entity, faction.Value, queue[0]);
                }
                else
                {
                    var position = SpawnPoint(entity, producer, transform, pad);
                    var unit = Spawn(prefab, position, transform, faction, hasRally.ValueRO, rally.Position);
                    DockOnPad(unit, entity, pad);
                    SpawnedPopulation[faction.Value] += producible.Population;
                    Produced[Events].Add(new UnitProduced
                    {
                        Producer = entity, Prefab = prefab, Faction = faction.Value, Position = position,
                    });
                }

                queue.RemoveAt(0);
                producer.Elapsed = 0f;
            }

            private void RecordResearch(Entity producer, byte faction, ProductionQueueItem item)
            {
                Researched[Events].Add(new ResearchCompleted
                {
                    Producer = producer, Prefab = item.Prefab, Faction = faction,
                });

                if (CompletedResearchLookup.TryGetBuffer(Players[faction], out var researched))
                {
                    researched.Add(new CompletedResearch { Prefab = item.Prefab, TypeId = item.TypeId });
                }
            }

            private bool HasRoomFor(byte faction, int population)
            {
                if (!PopulationLookup.TryGetComponent(Players[faction], out var current))
                {
                    return true;
                }

                current.Used += SpawnedPopulation[faction];
                return current.HasRoomFor(population);
            }

            /// <summary>Aircraft that use pads need a free one here; -1 for every other unit.</summary>
            private bool TryReservePad(Entity producer, Entity prefab, out int pad)
            {
                pad = -1;
                if (!PadUserLookup.HasComponent(prefab) || !PadLookup.TryGetBuffer(producer, out var pads))
                {
                    return true;
                }

                pad = LandingPad.FindFree(pads);
                return pad >= 0;
            }

            private float3 SpawnPoint(Entity entity, in Producer producer, in LocalTransform transform, int pad) =>
                transform.TransformPoint(pad >= 0 ? PadLookup[entity][pad].Offset : producer.SpawnOffset);

            /// <summary>PadSystem claims the pad next frame, once the unit exists.</summary>
            private void DockOnPad(Entity unit, Entity airfield, int pad)
            {
                if (pad >= 0)
                {
                    Ecb.SetComponent(unit, new HomePad { Airfield = airfield, Pad = pad });
                    Ecb.SetComponentEnabled<Docked>(unit, true);
                }
            }

            private Entity Spawn(Entity prefab, float3 position, in LocalTransform producer,
                in Faction faction, bool rally, float3 rallyPosition)
            {
                var unit = Ecb.Spawn(prefab, TransformLookup[prefab], StretchLookup, position, producer.Rotation,
                    faction.Value);

                if (rally && OrderLookup.HasComponent(prefab))
                {
                    var order = new Order { Type = OrderType.Move, Position = rallyPosition };
                    Ecb.SetComponent(unit, new ActiveOrder { Value = order });
                    Ecb.SetComponentEnabled<ActiveOrder>(unit, true);
                }

                return unit;
            }
        }
    }
}
