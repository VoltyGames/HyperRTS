using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// Flies aircraft straight at their <see cref="MoveDestination"/> at their <see cref="Flight"/> altitude, steering round other aircraft.
    /// Idle ones that can't hover circle.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    [UpdateBefore(typeof(CargoFollowSystem))]
    public partial struct FlightMovementSystem : ISystem
    {
        private RightOfWay _rightOfWay;
        private EntityQuery _fliers;

        public void OnCreate(ref SystemState state)
        {
            _rightOfWay = new RightOfWay(ref state);
            // Present, not enabled: arrival and facing are switched here, so disabled ones must still match.
            _fliers = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<LocalTransform>().WithAllRW<PathState>().WithAllRW<PathWaypoint>()
                .WithAllRW<MoveProgress>().WithAllRW<Velocity>()
                .WithAll<MovementSpeed>().WithAll<NavAgent>().WithAll<Owner>().WithAll<Flight>()
                .WithPresentRW<MoveDestination>().WithPresentRW<Arrival>().WithPresentRW<FacingGoal>().WithNone<Inside>().Build(ref state);
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _rightOfWay.Update(ref state, SystemAPI.GetSingleton<OwnerRelations>());
            var index = SystemAPI.GetSingleton<SpatialIndex>();
            var grid = SystemAPI.GetSingleton<NavGrid>();
            if (SystemAPI.Time.DeltaTime <= 0f)
            {
                return;
            }

            state.Dependency = new FlyJob
            {
                Mover = new FlightMover
                {
                    Movers = new Movers
                    {
                        Index = index,
                        RightOfWay = _rightOfWay,
                        Locomotions = SystemAPI.GetComponentLookup<Locomotion>(true),
                        DeltaTime = SystemAPI.Time.DeltaTime,
                    },
                    Grid = grid,
                    Docked = SystemAPI.GetComponentLookup<Docked>(true),
                },
                Entities = SystemAPI.GetEntityTypeHandle(),
                Transforms = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
                Speeds = SystemAPI.GetComponentTypeHandle<MovementSpeed>(true),
                Agents = SystemAPI.GetComponentTypeHandle<NavAgent>(true),
                Owners = SystemAPI.GetComponentTypeHandle<Owner>(true),
                Flights = SystemAPI.GetComponentTypeHandle<Flight>(true),
                Destinations = SystemAPI.GetComponentTypeHandle<MoveDestination>(),
                Paths = SystemAPI.GetComponentTypeHandle<PathState>(),
                Waypoints = SystemAPI.GetBufferTypeHandle<PathWaypoint>(),
                Progresses = SystemAPI.GetComponentTypeHandle<MoveProgress>(),
                Arrivals = SystemAPI.GetComponentTypeHandle<Arrival>(),
                Velocities = SystemAPI.GetComponentTypeHandle<Velocity>(),
                Aims = SystemAPI.GetComponentTypeHandle<FacingGoal>(),
                Drives = SystemAPI.GetComponentTypeHandle<LocomotionState>(),
            }.ScheduleParallel(_fliers, state.Dependency);
        }

        // One job for aircraft with and without a LocomotionState (those without start each frame from rest).
        [BurstCompile]
        private struct FlyJob : IJobChunk
        {
            public FlightMover Mover;
            [ReadOnly] public EntityTypeHandle Entities;
            public ComponentTypeHandle<LocalTransform> Transforms;
            [ReadOnly] public ComponentTypeHandle<MovementSpeed> Speeds;
            [ReadOnly] public ComponentTypeHandle<NavAgent> Agents;
            [ReadOnly] public ComponentTypeHandle<Owner> Owners;
            [ReadOnly] public ComponentTypeHandle<Flight> Flights;
            public ComponentTypeHandle<MoveDestination> Destinations;
            public ComponentTypeHandle<PathState> Paths;
            public BufferTypeHandle<PathWaypoint> Waypoints;
            public ComponentTypeHandle<MoveProgress> Progresses;
            public ComponentTypeHandle<Arrival> Arrivals;
            public ComponentTypeHandle<Velocity> Velocities;
            public ComponentTypeHandle<FacingGoal> Aims;
            public ComponentTypeHandle<LocomotionState> Drives;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(Entities);
                var transforms = chunk.GetNativeArray(ref Transforms).AsSpan();
                var speeds = chunk.GetNativeArray(ref Speeds);
                var agents = chunk.GetNativeArray(ref Agents);
                var owners = chunk.GetNativeArray(ref Owners);
                var flights = chunk.GetNativeArray(ref Flights);
                var destinations = chunk.GetNativeArray(ref Destinations).AsSpan();
                var paths = chunk.GetNativeArray(ref Paths).AsSpan();
                var waypoints = chunk.GetBufferAccessorRW(ref Waypoints);
                var progresses = chunk.GetNativeArray(ref Progresses).AsSpan();
                var arrivals = chunk.GetNativeArray(ref Arrivals).AsSpan();
                var velocities = chunk.GetNativeArray(ref Velocities).AsSpan();
                var aims = chunk.GetNativeArray(ref Aims).AsSpan();
                var movingMask = chunk.GetEnabledMask(ref Destinations);
                var arrivedMask = chunk.GetEnabledMask(ref Arrivals);
                var aimingMask = chunk.GetEnabledMask(ref Aims);
                var remembers = chunk.Has(ref Drives);
                var drives = remembers ? chunk.GetNativeArray(ref Drives) : default;
                for (var i = 0; i < chunk.Count; i++)
                {
                    var drive = remembers ? drives[i] : default;
                    Mover.Fly(entities[i], ref transforms[i], speeds[i], agents[i], owners[i], flights[i],
                        ref destinations[i], movingMask.GetEnabledRefRW<MoveDestination>(i),
                        ref paths[i], waypoints[i], ref progresses[i],
                        ref arrivals[i], arrivedMask.GetEnabledRefRW<Arrival>(i),
                        ref velocities[i], ref aims[i], aimingMask.GetEnabledRefRW<FacingGoal>(i),
                        ref drive, remembers);
                    if (remembers)
                    {
                        drives[i] = drive;
                    }
                }
            }
        }
    }
}
