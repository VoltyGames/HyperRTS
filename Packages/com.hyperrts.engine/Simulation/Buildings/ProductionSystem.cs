using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Upgrades;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Trains the head of each completed producer's queue while the owner has population room, then spawns it at the
    /// spawn offset and sends it to the rally point. A finished upgrade is recorded on the owner instead.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    [UpdateAfter(typeof(PopulationSystem))]
    public partial struct ProductionSystem : ISystem
    {
        private EntityQuery _players;
        private ComponentLookup<Population> _populationLookup;
        private ComponentLookup<Producible> _producibleLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ActiveOrder> _orderLookup;
        private ComponentLookup<Upgrade> _upgradeLookup;
        private ComponentLookup<Unpowered> _unpoweredLookup;
        private ComponentLookup<EntityInfo> _infoLookup;
        private BufferLookup<ResearchedUpgrade> _researchedLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _players = SystemAPI.QueryBuilder().WithAll<Player>().Build();
            _populationLookup = state.GetComponentLookup<Population>(true);
            _producibleLookup = state.GetComponentLookup<Producible>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _orderLookup = state.GetComponentLookup<ActiveOrder>(true);
            _upgradeLookup = state.GetComponentLookup<Upgrade>(true);
            _unpoweredLookup = state.GetComponentLookup<Unpowered>(true);
            _infoLookup = state.GetComponentLookup<EntityInfo>(true);
            _researchedLookup = state.GetBufferLookup<ResearchedUpgrade>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _populationLookup.Update(ref state);
            _producibleLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _orderLookup.Update(ref state);
            _upgradeLookup.Update(ref state);
            _unpoweredLookup.Update(ref state);
            _infoLookup.Update(ref state);
            _researchedLookup.Update(ref state);

            var allocator = state.WorldUpdateAllocator;
            var rules = SystemAPI.TryGetSingleton<MatchRules>(out var match) ? match : MatchRules.Default;
            new ProduceJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                LowPowerRate = rules.LowPowerProductionRate,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                PlayerByFaction = PlayerLookup.ByFaction(_players, allocator),
                SpawnedPopulation = CollectionHelper.CreateNativeArray<int>(PlayerLookup.Capacity, allocator),
                PopulationLookup = _populationLookup,
                ProducibleLookup = _producibleLookup,
                TransformLookup = _transformLookup,
                OrderLookup = _orderLookup,
                UpgradeLookup = _upgradeLookup,
                UnpoweredLookup = _unpoweredLookup,
                InfoLookup = _infoLookup,
                ResearchedLookup = _researchedLookup,
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
            [ReadOnly] public NativeArray<Entity> PlayerByFaction;
            public NativeArray<int> SpawnedPopulation;
            [ReadOnly] public ComponentLookup<Population> PopulationLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<ActiveOrder> OrderLookup;
            [ReadOnly] public ComponentLookup<Upgrade> UpgradeLookup;
            [ReadOnly] public ComponentLookup<Unpowered> UnpoweredLookup;
            [ReadOnly] public ComponentLookup<EntityInfo> InfoLookup;
            public BufferLookup<ResearchedUpgrade> ResearchedLookup;

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
                if (!HasRoomFor(faction.Value, producible.Population))
                {
                    return;
                }

                var lowPower = PowerRules.IsUnpowered(UnpoweredLookup, entity);
                producer.Elapsed += DeltaTime * producer.Speed * (lowPower ? LowPowerRate : 1f);
                if (producer.Elapsed < producible.BuildTime)
                {
                    return;
                }

                if (UpgradeLookup.HasComponent(prefab))
                {
                    Research(faction.Value, prefab);
                }
                else
                {
                    Spawn(prefab, transform.TransformPoint(producer.SpawnOffset), transform, faction,
                        hasRally.ValueRO, rally.Position);
                    SpawnedPopulation[faction.Value] += producible.Population;
                }

                queue.RemoveAt(0);
                producer.Elapsed = 0f;
            }

            private void Research(byte faction, Entity upgrade)
            {
                if (ResearchedLookup.TryGetBuffer(PlayerByFaction[faction], out var researched))
                {
                    researched.Add(new ResearchedUpgrade { Upgrade = upgrade, TypeId = InfoLookup[upgrade].TypeId });
                }
            }

            private bool HasRoomFor(byte faction, int population)
            {
                if (!PopulationLookup.TryGetComponent(PlayerByFaction[faction], out var current))
                {
                    return true;
                }

                current.Used += SpawnedPopulation[faction];
                return current.HasRoomFor(population);
            }

            private void Spawn(Entity prefab, float3 position, in LocalTransform producer,
                in Faction faction, bool rally, float3 rallyPosition)
            {
                var unit = Ecb.Instantiate(prefab);
                var placement = TransformLookup.TryGetComponent(prefab, out var prefabTransform)
                    ? prefabTransform
                    : LocalTransform.Identity;
                placement.Position = position;
                placement.Rotation = producer.Rotation;
                Ecb.AddComponent(unit, placement);
                Ecb.SetComponent(unit, faction);

                if (rally && OrderLookup.HasComponent(prefab))
                {
                    var order = new Order { Type = OrderType.Move, Position = rallyPosition };
                    Ecb.SetComponent(unit, new ActiveOrder { Value = order });
                    Ecb.SetComponentEnabled<ActiveOrder>(unit, true);
                }
            }
        }
    }
}
