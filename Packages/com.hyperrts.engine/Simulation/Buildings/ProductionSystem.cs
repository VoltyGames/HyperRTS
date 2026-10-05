using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Trains the head of each completed producer's queue while the owner has population room, then spawns it at the
    /// spawn offset and sends it to the rally point.
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

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _players = SystemAPI.QueryBuilder().WithAll<Player>().Build();
            _populationLookup = state.GetComponentLookup<Population>(true);
            _producibleLookup = state.GetComponentLookup<Producible>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _orderLookup = state.GetComponentLookup<ActiveOrder>(true);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _populationLookup.Update(ref state);
            _producibleLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _orderLookup.Update(ref state);

            var allocator = state.WorldUpdateAllocator;
            new ProduceJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                PlayerByFaction = PlayerLookup.ByFaction(_players, allocator),
                SpawnedPopulation = CollectionHelper.CreateNativeArray<int>(PlayerLookup.Capacity, allocator),
                PopulationLookup = _populationLookup,
                ProducibleLookup = _producibleLookup,
                TransformLookup = _transformLookup,
                OrderLookup = _orderLookup,
            }.Schedule();
        }

        /// <summary>Single-threaded so units finished this frame count against the population cap right away.</summary>
        [BurstCompile]
        [WithNone(typeof(ConstructionProgress), typeof(Dead))]
        [WithPresent(typeof(RallyPoint))]
        private partial struct ProduceJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer Ecb;
            [ReadOnly] public NativeArray<Entity> PlayerByFaction;
            public NativeArray<int> SpawnedPopulation;
            [ReadOnly] public ComponentLookup<Population> PopulationLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<ActiveOrder> OrderLookup;

            private void Execute(ref Producer producer, DynamicBuffer<ProductionQueueItem> queue,
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

                producer.Elapsed += DeltaTime;
                if (producer.Elapsed < producible.BuildTime)
                {
                    return;
                }

                Spawn(prefab, transform.TransformPoint(producer.SpawnOffset), transform, faction,
                    hasRally.ValueRO, rally.Position);
                SpawnedPopulation[faction.Value] += producible.Population;
                queue.RemoveAt(0);
                producer.Elapsed = 0f;
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
