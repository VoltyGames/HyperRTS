using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
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
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<PostTransformMatrix> _stretchLookup;
        private ComponentLookup<Offline> _offlineLookup;
        private ComponentLookup<PlayerSpawn> _spawnLookup;
        private ComponentLookup<Flight> _flightLookup;
        private ComponentLookup<AirdropReceiver> _receiverLookup;
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<Dead> _deadLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAny<AirdropReceiver, AirdropFlight>().Build());
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _stretchLookup = state.GetComponentLookup<PostTransformMatrix>(true);
            _offlineLookup = state.GetComponentLookup<Offline>(true);
            _spawnLookup = state.GetComponentLookup<PlayerSpawn>(true);
            _flightLookup = state.GetComponentLookup<Flight>(true);
            _receiverLookup = state.GetComponentLookup<AirdropReceiver>(true);
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _deadLookup = state.GetComponentLookup<Dead>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _stretchLookup.Update(ref state);
            _offlineLookup.Update(ref state);
            _spawnLookup.Update(ref state);
            _flightLookup.Update(ref state);
            _receiverLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _deadLookup.Update(ref state);
            var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var map = SystemAPI.GetSingleton<MapSettings>();

            new FlyJob
            {
                Map = map,
                Ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged),
                TransformLookup = _transformLookup,
                StretchLookup = _stretchLookup,
                ReceiverLookup = _receiverLookup,
                FactionLookup = _factionLookup,
                DeadLookup = _deadLookup,
            }.Schedule();

            new SendJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Map = map,
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                TransformLookup = _transformLookup,
                StretchLookup = _stretchLookup,
                OfflineLookup = _offlineLookup,
                SpawnLookup = _spawnLookup,
                FlightLookup = _flightLookup,
            }.ScheduleParallel();
        }

        /// <summary>Counts down each owned, working receiver and sends its aircraft in from the owner's edge.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct SendJob : IJobEntity
        {
            public float DeltaTime;
            public MapSettings Map;
            public PlayerRegistry Players;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<PostTransformMatrix> StretchLookup;
            [ReadOnly] public ComponentLookup<Offline> OfflineLookup;
            [ReadOnly] public ComponentLookup<PlayerSpawn> SpawnLookup;
            [ReadOnly] public ComponentLookup<Flight> FlightLookup;

            private void Execute([ChunkIndexInQuery] int chunk, Entity entity, ref AirdropReceiver receiver,
                in Faction faction)
            {
                if (faction.Value == Faction.Neutral)
                {
                    receiver.Elapsed = 0f;
                    return;
                }

                if (OfflineLookup.HasEnabled(entity))
                {
                    return;
                }

                if (IntervalTimer.Advance(ref receiver.Elapsed, receiver.Interval, DeltaTime) == 0)
                {
                    return;
                }

                var start = SpawnLookup[Players[faction.Value]].Position;
                var landing = TransformLookup[entity].TransformPoint(receiver.Landing);
                var route = DropRoute.Plan(Map, start, landing, receiver.Altitude);
                var aircraft = Ecb.Spawn(chunk, receiver.Aircraft, TransformLookup[receiver.Aircraft], StretchLookup,
                    route.Entry, route.Facing, faction.Value);
                var flight = FlightLookup[receiver.Aircraft];
                flight.Altitude = receiver.Altitude;
                Ecb.SetComponent(chunk, aircraft, flight);
                Ecb.AddComponent(chunk, aircraft, new AirdropFlight { Receiver = entity, Route = route });
                Ecb.SetComponent(chunk, aircraft, new MoveDestination { Value = route.Target });
                Ecb.SetComponentEnabled<MoveDestination>(chunk, aircraft, true);
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
            [ReadOnly] public ComponentLookup<PostTransformMatrix> StretchLookup;
            [ReadOnly] public ComponentLookup<AirdropReceiver> ReceiverLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public ComponentLookup<Dead> DeadLookup;

            private void Execute(Entity entity, ref AirdropFlight flight, ref DropRun run,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, in MovementSpeed speed,
                in Faction faction)
            {
                var transform = TransformLookup[entity];
                if (flight.Leaving && !Map.Contains(transform.Position))
                {
                    Ecb.DestroyEntity(entity);
                    return;
                }

                var release = transform.TransformPoint(run.ReleasePoint);
                run.Track(flight.Route.DistanceToTarget(release), speed.Value);
                if (!flight.Leaving)
                {
                    Approach(ref flight, release, faction.Value);
                }

                destination.Value = flight.Route.Destination(flight.Leaving, run.Behind(transform.Scale));
                moving.ValueRW = true;
            }

            private void Approach(ref AirdropFlight flight, float3 release, byte faction)
            {
                if (!IsWorking(flight.Receiver, faction, out var receiver))
                {
                    flight.Leaving = true;
                    return;
                }

                if (!flight.Route.IsReached(release))
                {
                    return;
                }

                flight.Leaving = true;
                var prefabTransform = TransformLookup[receiver.Crate];
                var crate = Ecb.Spawn(receiver.Crate, prefabTransform, StretchLookup, release,
                    prefabTransform.Rotation, faction);
                Ecb.AddComponent(crate, new AirdropCrate
                {
                    Receiver = flight.Receiver, Type = receiver.Type, Amount = receiver.Amount,
                    FallSpeed = receiver.FallSpeed, Ground = flight.Route.Target.y,
                });
            }

            /// <summary>Still standing and still the aircraft owner's; a lost or captured receiver gets nothing.</summary>
            private bool IsWorking(Entity entity, byte faction, out AirdropReceiver receiver)
            {
                if (!ReceiverLookup.TryGetComponent(entity, out receiver) || DeadLookup.HasEnabled(entity))
                {
                    return false;
                }

                return FactionLookup[entity].Value == faction;
            }
        }
    }
}
