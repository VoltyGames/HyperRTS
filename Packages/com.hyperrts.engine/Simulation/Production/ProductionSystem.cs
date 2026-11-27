using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Common.Systems;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Trains the head of each completed producer's queue while the owner has population room, then spawns it and sends it to the rally point.
    /// Finished research is recorded on the owner.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    [UpdateAfter(typeof(PopulationSystem))]
    public partial struct ProductionSystem : ISystem
    {
        private EntityQuery _producers;
        private PrefabSpawner _spawner;
        private AirfieldPads _pads;
        private EventWriter<UnitProduced> _produced;
        private EventWriter<ResearchCompleted> _researched;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _producers = new EntityQueryBuilder(Allocator.Temp).WithAll<Producer, ProductionQueueItem, LocalTransform, Owner>()
                .WithPresent<RallyPoint>().WithNone<ConstructionProgress>().Build(ref state);
            _spawner = new PrefabSpawner(ref state);
            _pads = new AirfieldPads(ref state);
            _produced = new EventWriter<UnitProduced>(ref state);
            _researched = new EventWriter<ResearchCompleted>(ref state);
            state.RequireForUpdate<MatchRules>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawner.Update(ref state);
            _pads.Update(ref state);
            _produced.Update(ref state);
            _researched.Update(ref state);

            var changes = new NativeList<ProducerChange>(state.WorldUpdateAllocator);
            state.Dependency = new ProduceJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                LowPowerRate = SystemAPI.GetSingleton<MatchRules>().LowPowerProductionRate,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                SpawnedPopulation = CollectionHelper.CreateNativeArray<int>(PlayerRegistry.Capacity, state.WorldUpdateAllocator),
                PopulationLookup = SystemAPI.GetComponentLookup<Population>(true),
                ProducibleLookup = SystemAPI.GetComponentLookup<Producible>(true),
                Spawner = _spawner,
                ResearchLookup = SystemAPI.GetComponentLookup<Research>(true),
                OfflineLookup = SystemAPI.GetComponentLookup<Offline>(true),
                CompletedResearchLookup = SystemAPI.GetBufferLookup<CompletedResearch>(false),
                Pads = _pads,
                Produced = _produced,
                Researched = _researched,
                Entities = SystemAPI.GetEntityTypeHandle(),
                Producers = SystemAPI.GetComponentTypeHandle<Producer>(true),
                Queues = SystemAPI.GetBufferTypeHandle<ProductionQueueItem>(true),
                Changes = changes,
                Transforms = SystemAPI.GetComponentTypeHandle<LocalTransform>(true),
                Owners = SystemAPI.GetComponentTypeHandle<Owner>(true),
                Rallies = SystemAPI.GetComponentTypeHandle<RallyPoint>(true),
            }.Schedule(_producers, state.Dependency);
            state.Dependency = new ApplyJob
            {
                Changes = changes,
                Producers = SystemAPI.GetComponentLookup<Producer>(),
                Queues = SystemAPI.GetBufferLookup<ProductionQueueItem>(),
            }.Schedule(state.Dependency);
        }

        /// <summary>A producer whose state changed this frame, and whether its head item left the queue.</summary>
        private struct ProducerChange
        {
            public Entity Entity;
            public Producer Producer;
            public bool Finished;
        }

        /// <summary>Writes only the producers that changed, so idle ones keep their change version.</summary>
        [BurstCompile]
        private struct ApplyJob : IJob
        {
            [ReadOnly] public NativeList<ProducerChange> Changes;
            public ComponentLookup<Producer> Producers;
            public BufferLookup<ProductionQueueItem> Queues;

            public void Execute()
            {
                foreach (var change in Changes)
                {
                    Producers[change.Entity] = change.Producer;
                    if (change.Finished)
                    {
                        Queues[change.Entity].RemoveAt(0);
                    }
                }
            }
        }

        /// <summary>Single-threaded so units finished this frame count against the population cap right away.</summary>
        [BurstCompile]
        private struct ProduceJob : IJobChunk
        {
            public float DeltaTime;
            public float LowPowerRate;
            public EntityCommandBuffer Ecb;
            public PlayerRegistry Players;
            public NativeArray<int> SpawnedPopulation;
            [ReadOnly] public ComponentLookup<Population> PopulationLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            public PrefabSpawner Spawner;
            [ReadOnly] public ComponentLookup<Research> ResearchLookup;
            [ReadOnly] public ComponentLookup<Offline> OfflineLookup;
            public BufferLookup<CompletedResearch> CompletedResearchLookup;
            public AirfieldPads Pads;
            public EventWriter<UnitProduced> Produced;
            public EventWriter<ResearchCompleted> Researched;

            [ReadOnly] public EntityTypeHandle Entities;
            [ReadOnly] public ComponentTypeHandle<Producer> Producers;
            [ReadOnly] public ComponentTypeHandle<LocalTransform> Transforms;
            [ReadOnly] public ComponentTypeHandle<Owner> Owners;
            [ReadOnly] public ComponentTypeHandle<RallyPoint> Rallies;

            [ReadOnly] public BufferTypeHandle<ProductionQueueItem> Queues;
            public NativeList<ProducerChange> Changes;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(Entities);
                var producers = chunk.GetNativeArray(ref Producers);
                var queues = chunk.GetBufferAccessorRO(ref Queues);
                var transforms = chunk.GetNativeArray(ref Transforms);
                var owners = chunk.GetNativeArray(ref Owners);
                var rallies = chunk.GetNativeArray(ref Rallies);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var before = producers[i];
                    var producer = before;
                    var input = new ProducerInput
                    {
                        Entity = entities[i],
                        Transform = transforms[i],
                        Owner = owners[i],
                        Rally = rallies[i],
                        HasRally = chunk.IsComponentEnabled(ref Rallies, i),
                    };
                    var finished = Work(input, ref producer, queues[i]);
                    var progressed = producer.Elapsed != before.Elapsed || producer.Released != before.Released;
                    if (finished || progressed || producer.State != before.State)
                    {
                        Changes.Add(new ProducerChange { Entity = entities[i], Producer = producer, Finished = finished });
                    }
                }
            }

            /// <summary>Whether the head of the queue finished and must leave it; sets the producer's state.</summary>
            private bool Work(in ProducerInput input, ref Producer producer,
                in DynamicBuffer<ProductionQueueItem> queue)
            {
                if (queue.IsEmpty)
                {
                    producer.Elapsed = 0f;
                    producer.State = ProducerState.Idle;
                    return false;
                }

                var producible = ProducibleLookup[queue[0].Prefab];
                producer.State = FindState(input, queue[0].Prefab, producible.Population, out var pad);
                if (producer.State != ProducerState.Working)
                {
                    return false;
                }

                var lowPower = (FindOffline(input.Entity).Reasons & OfflineReason.Unpowered) != 0;
                producer.Elapsed += DeltaTime * producer.Speed * (lowPower ? LowPowerRate : 1f);
                if (producer.Elapsed < producible.BuildTime)
                {
                    return false;
                }

                Deliver(input, ref producer, queue[0], producible, pad);
                producer.Elapsed = 0f;
                return true;
            }

            /// <summary>What stops work on the head of a non-empty queue, or Working, with the aircraft's pad.</summary>
            private ProducerState FindState(in ProducerInput input, Entity prefab, int population, out int pad)
            {
                pad = -1;
                if (!HasRoomFor(input.Owner.Value, population))
                {
                    return ProducerState.WaitingPopulation;
                }

                if (!Pads.TryReserve(input.Entity, prefab, out pad))
                {
                    return ProducerState.WaitingPad;
                }

                // Low power only slows production; any other reason (an EMP) stops it.
                var stopped = FindOffline(input.Entity).HasReasonOtherThan(OfflineReason.Unpowered);
                return stopped ? ProducerState.Offline : ProducerState.Working;
            }

            private Offline FindOffline(Entity entity) =>
                OfflineLookup.TryGetComponent(entity, out var found) ? found : default;

            private void Deliver(in ProducerInput input, ref Producer producer, in ProductionQueueItem item,
                in Producible producible, int pad)
            {
                var entity = input.Entity;
                var prefab = item.Prefab;
                var owner = input.Owner.Value;
                if (ResearchLookup.HasComponent(prefab))
                {
                    RecordResearch(entity, owner, item);
                    return;
                }

                var position = Pads.SpawnPoint(entity, pad, producer.SpawnOffset, input.Transform);
                var unit = Spawn(prefab, position, input);
                Pads.Dock(Ecb, unit, prefab, entity, pad);
                SpawnedPopulation[owner] += producible.Population;
                producer.Released++;
                Produced.Add(new UnitProduced
                {
                    Producer = entity, Prefab = prefab, Owner = owner, Position = position,
                });
            }

            private void RecordResearch(Entity producer, byte owner, ProductionQueueItem item)
            {
                Researched.Add(new ResearchCompleted
                {
                    Producer = producer, Prefab = item.Prefab, Owner = owner,
                });

                if (CompletedResearchLookup.TryGetBuffer(Players[owner], out var researched))
                {
                    researched.Add(new CompletedResearch { Prefab = item.Prefab, TypeId = item.TypeId });
                }
            }

            private bool HasRoomFor(byte owner, int population)
            {
                var current = PopulationLookup[Players[owner]];
                current.Used += SpawnedPopulation[owner];
                return current.HasRoomFor(population);
            }

            private Entity Spawn(Entity prefab, float3 position, in ProducerInput producer)
            {
                var unit = Spawner.Spawn(Ecb, prefab, position, producer.Transform.Rotation, producer.Owner.Value);

                if (producer.HasRally)
                {
                    Ecb.AddComponent(unit, new RallyOrder { Position = producer.Rally.Position });
                }

                return unit;
            }
        }
    }
}
