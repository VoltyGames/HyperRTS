using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Match;
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
        private EventWriter<UnitProduced> _produced;
        private EventWriter<ResearchCompleted> _researched;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _producers = new EntityQueryBuilder(Allocator.Temp).WithAll<Producer, ProductionQueueItem, LocalTransform, Owner>()
                .WithPresent<RallyPoint>().WithNone<ConstructionProgress, Dead>()
                .Build(ref state);
            _spawner = new PrefabSpawner(ref state);
            _produced = new EventWriter<UnitProduced>(ref state);
            _researched = new EventWriter<ResearchCompleted>(ref state);
            state.RequireForUpdate<MatchRules>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawner.Update(ref state);
            _produced.Update(ref state);
            _researched.Update(ref state);

            var allocator = state.WorldUpdateAllocator;
            var changes = new NativeList<ProducerChange>(allocator);
            state.Dependency = new ProduceJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                LowPowerRate = SystemAPI.GetSingleton<MatchRules>().LowPowerProductionRate,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                SpawnedPopulation = CollectionHelper.CreateNativeArray<int>(PlayerRegistry.Capacity, allocator),
                PopulationLookup = SystemAPI.GetComponentLookup<Population>(true),
                ProducibleLookup = SystemAPI.GetComponentLookup<Producible>(true),
                Spawner = _spawner,
                ResearchLookup = SystemAPI.GetComponentLookup<Research>(true),
                OfflineLookup = SystemAPI.GetComponentLookup<Offline>(true),
                CompletedResearchLookup = SystemAPI.GetBufferLookup<CompletedResearch>(false),
                PadLookup = SystemAPI.GetBufferLookup<LandingPad>(true),
                PadUserLookup = SystemAPI.GetComponentLookup<HomePad>(true),
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
            [ReadOnly] public BufferLookup<LandingPad> PadLookup;
            [ReadOnly] public ComponentLookup<HomePad> PadUserLookup;
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
                    var hasRally = chunk.IsComponentEnabled(ref Rallies, i);
                    var finished = Work(entities[i], ref producer, queues[i], transforms[i], owners[i], rallies[i],
                        hasRally);
                    if (finished || producer.Elapsed != before.Elapsed || producer.Released != before.Released)
                    {
                        Changes.Add(new ProducerChange { Entity = entities[i], Producer = producer, Finished = finished });
                    }
                }
            }

            /// <summary>Whether the head of the queue finished and must leave it.</summary>
            private bool Work(Entity entity, ref Producer producer, in DynamicBuffer<ProductionQueueItem> queue,
                in LocalTransform transform, in Owner owner, in RallyPoint rally, bool hasRally)
            {
                if (queue.IsEmpty)
                {
                    producer.Elapsed = 0f;
                    return false;
                }

                var prefab = queue[0].Prefab;
                var producible = ProducibleLookup[prefab];
                if (!HasRoomFor(owner.Value, producible.Population) || !TryReservePad(entity, prefab, out var pad))
                {
                    return false;
                }

                // Low power only slows production; any other reason (an EMP) halts it.
                var offline = OfflineLookup.TryGetComponent(entity, out var found) ? found : default;
                if (offline.HasReasonOtherThan(OfflineReason.Unpowered))
                {
                    return false;
                }

                var lowPower = (offline.Reasons & OfflineReason.Unpowered) != 0;
                producer.Elapsed += DeltaTime * producer.Speed * (lowPower ? LowPowerRate : 1f);
                if (producer.Elapsed < producible.BuildTime)
                {
                    return false;
                }

                if (ResearchLookup.HasComponent(prefab))
                {
                    RecordResearch(entity, owner.Value, queue[0]);
                }
                else
                {
                    var position = SpawnPoint(entity, producer, transform, pad);
                    var unit = Spawn(prefab, position, transform, owner, hasRally, rally.Position);
                    DockOnPad(unit, prefab, entity, pad);
                    SpawnedPopulation[owner.Value] += producible.Population;
                    producer.Released++;
                    Produced.Add(new UnitProduced
                    {
                        Producer = entity, Prefab = prefab, Owner = owner.Value, Position = position,
                    });
                }

                producer.Elapsed = 0f;
                return true;
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

            /// <summary>Aircraft that use pads need a free one of their kind here; -1 for every other unit.</summary>
            private bool TryReservePad(Entity producer, Entity prefab, out int pad)
            {
                pad = -1;
                if (!PadUserLookup.HasComponent(prefab) || !PadLookup.TryGetBuffer(producer, out var pads))
                {
                    return true;
                }

                pad = LandingPad.FindFree(pads, PadUserLookup[prefab].Kind);
                return pad >= 0;
            }

            private float3 SpawnPoint(Entity entity, in Producer producer, in LocalTransform transform, int pad) =>
                transform.TransformPoint(pad >= 0 ? PadLookup[entity][pad].Offset : producer.SpawnOffset);

            /// <summary>PadSystem claims the pad next frame, once the unit exists.</summary>
            private void DockOnPad(Entity unit, Entity prefab, Entity airfield, int pad)
            {
                if (pad >= 0)
                {
                    Ecb.SetComponent(unit, PadUserLookup[prefab].ToPad(airfield, pad));
                    Ecb.SetComponentEnabled<Docked>(unit, true);
                }
            }

            private Entity Spawn(Entity prefab, float3 position, in LocalTransform producer,
                in Owner owner, bool rally, float3 rallyPosition)
            {
                var unit = Spawner.Spawn(Ecb, prefab, position, producer.Rotation, owner.Value);

                if (rally)
                {
                    Ecb.AddComponent(unit, new RallyOrder { Position = rallyPosition });
                }

                return unit;
            }
        }
    }
}
