using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Movement;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Airdrops
{
    /// <summary>
    /// Sends each working <see cref="AirdropReceiver"/>'s aircraft on its <see cref="DropRoute"/> and flies every
    /// <see cref="AirdropFlight"/>: it drops a crate over its receiver, turns away without one if the receiver is
    /// lost, and despawns once off the map.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct AirdropSystem : ISystem
    {
        private PrefabSpawner _spawner;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAny<AirdropReceiver, AirdropFlight>().Build());
            _spawner = new PrefabSpawner(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawner.Update(ref state);
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var map = SystemAPI.GetSingleton<MapSettings>();

            new FlyJob
            {
                Map = map,
                Ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Spawner = _spawner,
                ReceiverLookup = SystemAPI.GetComponentLookup<AirdropReceiver>(true),
                FactionLookup = SystemAPI.GetComponentLookup<Faction>(true),
                DeadLookup = SystemAPI.GetComponentLookup<Dead>(true),
            }.Schedule();

            new SendJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Map = map,
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Spawner = _spawner,
                OfflineLookup = SystemAPI.GetComponentLookup<Offline>(true),
                SpawnLookup = SystemAPI.GetComponentLookup<PlayerSpawn>(true),
                FlightLookup = SystemAPI.GetComponentLookup<Flight>(true),
            }.Schedule();
        }

        /// <summary>Counts down each owned, working receiver and sends its aircraft in from the owner's edge.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct SendJob : IJobEntity
        {
            public float DeltaTime;
            public MapSettings Map;
            public PlayerRegistry Players;
            public EntityCommandBuffer Ecb;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public PrefabSpawner Spawner;
            [ReadOnly] public ComponentLookup<Offline> OfflineLookup;
            [ReadOnly] public ComponentLookup<PlayerSpawn> SpawnLookup;
            [ReadOnly] public ComponentLookup<Flight> FlightLookup;

            private void Execute(Entity entity, ref AirdropReceiver receiver, in Faction faction)
            {
                var offline = OfflineLookup.HasEnabled(entity);
                if (IntervalTimer.Due(ref receiver.Elapsed, receiver.Interval, DeltaTime, faction.Value, offline) == 0)
                {
                    return;
                }

                var start = SpawnLookup[Players[faction.Value]].Position;
                var landing = TransformLookup[entity].TransformPoint(receiver.Landing);
                var route = DropRoute.Plan(Map, start, landing, receiver.Altitude);
                var aircraft = route.Launch(Ecb, Spawner, receiver.Aircraft, FlightLookup[receiver.Aircraft],
                    receiver.Altitude, faction.Value);
                Ecb.AddComponent(aircraft, new AirdropFlight { Receiver = entity });
            }
        }

        /// <summary>Single-threaded: drops spawn crates through one buffer.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination))]
        private partial struct FlyJob : IJobEntity
        {
            public MapSettings Map;
            public EntityCommandBuffer Ecb;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public PrefabSpawner Spawner;
            [ReadOnly] public ComponentLookup<AirdropReceiver> ReceiverLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public ComponentLookup<Dead> DeadLookup;

            private void Execute(Entity entity, ref DropFlight flight, in AirdropFlight delivery, ref DropRun run,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, in MovementSpeed speed,
                in Faction faction)
            {
                var transform = TransformLookup[entity];
                if (flight.HasLeft(Map, transform.Position))
                {
                    Ecb.DestroyEntity(entity);
                    return;
                }

                flight.Leaving |= !IsWorking(delivery.Receiver, faction.Value);
                if (flight.Fly(transform, speed.Value, ref run, ref destination, out var release))
                {
                    var crate = ReceiverLookup[delivery.Receiver];
                    var spawned = Spawner.Spawn(Ecb, crate.Crate, release, faction.Value);
                    Ecb.AddComponent(spawned, new AirdropCrate
                    {
                        Receiver = delivery.Receiver, FallSpeed = crate.FallSpeed, Ground = flight.Route.Target.y,
                    });
                }

                moving.ValueRW = true;
            }

            /// <summary>Still standing and still the aircraft owner's; a lost or captured one gets nothing.</summary>
            private bool IsWorking(Entity receiver, byte faction) =>
                ReceiverLookup.HasComponent(receiver) && !DeadLookup.HasEnabled(receiver)
                && FactionLookup[receiver].Value == faction;
        }
    }
}
