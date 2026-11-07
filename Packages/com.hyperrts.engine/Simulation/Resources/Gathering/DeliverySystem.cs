using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Resources.Gathering
{
    /// <summary>
    /// Sends <see cref="DeliverySource"/> waves from the map edge and pays each <see cref="DeliveryCarrier"/> that
    /// reaches its source, scaled by the owner's <c>Stat.Income</c>. Killed carriers pay nothing; carriers whose
    /// source is gone are removed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct DeliverySystem : ISystem
    {
        private PrefabSpawner _spawner;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            // Carriers still arrive after their source dies, so either keeps the system running.
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAny<DeliverySource, DeliveryCarrier>().Build());
            _spawner = new PrefabSpawner(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawner.Update(ref state);
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

            new ArriveJob
            {
                Ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged),
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                SourceLookup = SystemAPI.GetComponentLookup<DeliverySource>(true),
                DeadLookup = SystemAPI.GetComponentLookup<Dead>(true),
                StockLookup = SystemAPI.GetBufferLookup<ResourceStock>(false),
                StatsLookup = SystemAPI.GetComponentLookup<PlayerStats>(false),
                ModifierLookup = SystemAPI.GetBufferLookup<StatModifier>(true),
            }.Schedule();

            new SendJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Map = SystemAPI.GetSingleton<MapSettings>(),
                Ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Spawner = _spawner,
                OfflineLookup = SystemAPI.GetComponentLookup<Offline>(true),
            }.ScheduleParallel();
        }

        /// <summary>Counts down each owned, online source and spawns a wave heading for it.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct SendJob : IJobEntity
        {
            public float DeltaTime;
            public MapSettings Map;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public PrefabSpawner Spawner;
            [ReadOnly] public ComponentLookup<Offline> OfflineLookup;

            private void Execute([ChunkIndexInQuery] int chunk, Entity entity, ref DeliverySource source,
                in Faction faction)
            {
                var offline = OfflineLookup.HasEnabled(entity);
                // One wave even if a long frame covered several intervals: carriers spawned together would overlap.
                if (IntervalTimer.Due(ref source.Elapsed, source.Interval, DeltaTime, faction.Value, offline) == 0)
                {
                    return;
                }

                var transform = TransformLookup[entity];
                var outward = DeliveryMath.Outward(Map, transform.Position, transform.Rotation);
                var edge = DeliveryMath.EdgePoint(Map, outward);
                for (var i = 0; i < source.Count; i++)
                {
                    var position = DeliveryMath.WavePosition(Map, edge, outward, i, source.Count, source.Spacing);
                    Spawn(chunk, entity, source, position, transform.Position, faction);
                }
            }

            private void Spawn(int chunk, Entity target, in DeliverySource source, float3 position, float3 goal,
                in Faction faction)
            {
                var heading = new float3(goal.x - position.x, 0f, goal.z - position.z);
                var carrier = Spawner.Spawn(Ecb, chunk, source.Carrier, position,
                    quaternion.LookRotationSafe(heading, math.up()), faction.Value);
                Ecb.AddComponent(chunk, carrier, new DeliveryCarrier
                {
                    Source = target, Type = source.Type, Amount = source.Amount,
                });
                Ecb.SetComponent(chunk, carrier, new MoveDestination { Value = goal });
                Ecb.SetComponentEnabled<MoveDestination>(chunk, carrier, true);
            }
        }

        /// <summary>Single-threaded: several carriers pay into one player's stock.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct ArriveJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            public PlayerRegistry Players;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<DeliverySource> SourceLookup;
            [ReadOnly] public ComponentLookup<Dead> DeadLookup;
            public BufferLookup<ResourceStock> StockLookup;
            public ComponentLookup<PlayerStats> StatsLookup;
            [ReadOnly] public BufferLookup<StatModifier> ModifierLookup;

            private void Execute(Entity entity, in DeliveryCarrier carrier, in Faction faction)
            {
                var sourceGone = !SourceLookup.TryGetComponent(carrier.Source, out var source)
                                 || DeadLookup.HasEnabled(carrier.Source);
                if (sourceGone)
                {
                    Ecb.DestroyEntity(entity);
                    return;
                }

                var position = TransformLookup[entity].Position;
                var target = TransformLookup[carrier.Source].Position;
                if (math.distance(position.xz, target.xz) > source.ArrivalRange)
                {
                    return;
                }

                Pay(carrier, faction);
                Ecb.DestroyEntity(entity);
            }

            private void Pay(in DeliveryCarrier carrier, in Faction faction)
            {
                var player = faction.Value == Faction.Neutral ? Entity.Null : Players[faction.Value];
                var paid = ResourcePayout.Scaled(ModifierLookup, player, carrier.Amount);
                ResourcePayout.Pay(ref StockLookup, ref StatsLookup, player, carrier.Type, paid);
            }
        }
    }
}
