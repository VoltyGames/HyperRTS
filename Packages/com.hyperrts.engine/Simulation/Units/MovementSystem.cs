using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Follows <see cref="PathWaypoint"/>s toward an enabled <see cref="MoveDestination"/> and, when the move ends by
    /// itself, disables it and enables <see cref="Arrival"/>. <see cref="MoveProgress"/> repairs the path and gives
    /// up when the unit stops getting on. Keeps units of the same layer their spacing apart, slides ground units round
    /// blockers and never pushes a footprint into cells closed to the agent (<see cref="NavCapsule.Constrain"/>).
    /// Every unit drives the path with its <see cref="Locomotion"/>, or <see cref="Locomotion.Instant"/> without one.
    /// Steers on XZ, and Y follows the layer's surface on the <see cref="NavGrid"/> (ground, deck or water). Aircraft
    /// ignore the grid, climb to their <see cref="Flight"/> altitude (land while <see cref="Docked"/>) and, if they
    /// can't hover, circle when idle.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    [UpdateBefore(typeof(CargoFollowSystem))]
    public partial struct MovementSystem : ISystem
    {
        private EntityQuery _blockers;
        private ComponentLookup<Flight> _flights;
        private GivingWay _givingWay;
        private ComponentLookup<Locomotion> _locomotions;
        private ComponentLookup<LocomotionState> _locomotionStates;

        public void OnCreate(ref SystemState state)
        {
            _flights = state.GetComponentLookup<Flight>(true);
            _givingWay = new GivingWay(ref state);
            _locomotions = state.GetComponentLookup<Locomotion>(true);
            _locomotionStates = state.GetComponentLookup<LocomotionState>();
            _blockers = SystemAPI.QueryBuilder().WithPresent<Blocker>().Build();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _flights.Update(ref state);
            _givingWay.Update(ref state);
            _locomotions.Update(ref state);
            _locomotionStates.Update(ref state);
            var index = SystemAPI.GetSingleton<SpatialIndex>();
            var grid = SystemAPI.GetSingleton<NavGrid>();
            new MoveJob
            {
                Index = index,
                Grid = grid,
                // Asked of the query, not the index, which its builder is still filling.
                HasBlockers = !_blockers.IsEmptyIgnoreFilter,
                Flights = _flights,
                GivingWay = _givingWay,
                Locomotions = _locomotions,
                States = _locomotionStates,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Inside))]
        [WithPresent(typeof(MoveDestination), typeof(Arrival))]
        private partial struct MoveJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            [ReadOnly] public NavGrid Grid;
            public bool HasBlockers;
            [ReadOnly] public ComponentLookup<Flight> Flights;
            public GivingWay GivingWay;
            [ReadOnly] public ComponentLookup<Locomotion> Locomotions;

            // Each unit writes only its own state.
            [NativeDisableParallelForRestriction] public ComponentLookup<LocomotionState> States;
            public float DeltaTime;

            // `ref`, not `in`: each must share one writable handle with its EnabledRefRW.
            private void Execute(Entity entity, ref LocalTransform transform, in MovementSpeed speed, in NavAgent agent,
                in Faction faction, ref MoveDestination destination, EnabledRefRW<MoveDestination> moving,
                ref PathState path, DynamicBuffer<PathWaypoint> waypoints, ref MoveProgress progress,
                ref Arrival arrival, EnabledRefRW<Arrival> arrived)
            {
                var maxStep = speed.Value * DeltaTime;
                var heading = float2.zero;
                var locomotion = Locomotions.TryGetComponent(entity, out var own) ? own : Locomotion.Instant;
                // Instant handling needs no memory between frames, so units without it start each from rest.
                var remembers = States.TryGetComponent(entity, out var drive);
                // Read before arrival can clear it, as the index saw it, so both sides of a contact agree.
                var wasMoving = moving.ValueRO;
                var step = Plan(entity, transform, speed.Value, agent, destination.Value, moving, ref path, waypoints,
                    locomotion, ref drive, ref heading);

                var flying = agent.Layer == NavLayer.Air;
                var from = transform.Position.xz;
                var axis = agent.SegmentAxis(transform.Rotation);
                var crowd = new SeparationVisitor(entity, from, axis, agent, faction.Value, wasMoving, step,
                    destination, GivingWay);
                var wanted = from + step + Separation(ref crowd, maxStep);
                var stalled = false;
                if (flying)
                {
                    Fly(entity, ref transform, wanted);
                }
                else
                {
                    wanted = Clear(crowd, wanted);
                    Walk(ref transform, from, wanted, agent, axis);
                    stalled = moving.ValueRO && !crowd.ReachedGroup &&
                              progress.HasGivenUp(transform, agent, destination.Value, waypoints, ref path, DeltaTime);
                }

                if (moving.ValueRO && (crowd.ReachedGroup || stalled))
                {
                    Halt(moving, ref path, waypoints);
                }

                Report(wasMoving, moving.ValueRO, stalled, ref arrival, arrived, ref progress);
                if (remembers)
                {
                    drive.Hit(transform.Position.xz - from, step, !transform.Position.xz.Equals(wanted));
                    States[entity] = drive;
                }

                Face(ref transform, agent, axis, heading, flying);
            }

            private readonly void Face(ref LocalTransform transform, in NavAgent agent, float2 axis, float2 heading,
                bool flying)
            {
                var turned = quaternion.LookRotationSafe(new float3(heading.x, 0f, heading.y), math.up());
                if (math.any(heading != 0f) && (flying || CanTurn(transform, agent, axis, turned)))
                {
                    transform.Rotation = turned;
                }
            }

            private readonly void Walk(ref LocalTransform transform, float2 from, float2 wanted, in NavAgent agent,
                float2 axis)
            {
                transform.Position.xz = new NavCapsule(agent.Access(Grid), axis).Constrain(from, wanted);
                transform.Position.y = Grid.HeightFor(transform.Position, agent.Layer);
            }

            // Measured where the unit is headed and left unclamped, so it never sinks into a blocker even for a frame.
            private readonly float2 Clear(SeparationVisitor crowd, float2 wanted)
            {
                if (!HasBlockers)
                {
                    return wanted;
                }

                crowd.Position = wanted;
                crowd.Push = float2.zero;
                Index.QueryBlockers(crowd.Center, crowd.Reach, ref crowd);
                return wanted + crowd.Push;
            }

            private static void Halt(EnabledRefRW<MoveDestination> moving, ref PathState path,
                DynamicBuffer<PathWaypoint> waypoints)
            {
                moving.ValueRW = false;
                path.Status = PathStatus.None;
                path.ClearWaypoints(waypoints);
            }

            /// <summary>A move that just ended arrived or stalled; the next one measures progress afresh.</summary>
            private static void Report(bool wasMoving, bool isMoving, bool stalled, ref Arrival arrival,
                EnabledRefRW<Arrival> arrived, ref MoveProgress progress)
            {
                if (isMoving)
                {
                    arrived.ValueRW = false;
                    return;
                }

                progress.IsTracking = false;
                if (wasMoving)
                {
                    arrived.ValueRW = true;
                    arrival.Stalled = stalled;
                }
            }

            // Facing snaps to the heading, so a long hull mustn't swing an end into a closed cell; it moves on unturned.
            private readonly bool CanTurn(in LocalTransform transform, in NavAgent agent, float2 axis, quaternion turned)
            {
                if (agent.HalfSegment <= 0f)
                {
                    return true;
                }

                var capsule = new NavCapsule(agent.Access(Grid), axis);
                return capsule.CanTurn(transform.Position.xz, agent.SegmentAxis(turned));
            }

            /// <summary>Aircraft go wherever they steer and climb toward their altitude over the surface below.</summary>
            private readonly void Fly(Entity entity, ref LocalTransform transform, float2 wanted)
            {
                transform.Position.xz = wanted;
                var surface = Grid.SurfaceHeight(transform.Position);
                transform.Position.y = Flights.TryGetComponent(entity, out var flight)
                    ? FlightMath.Climb(transform.Position.y, surface, flight, IsLanded(entity), DeltaTime)
                    : surface;
            }

            /// <summary>An idle aircraft that can't hover keeps flying a circle; one that can stays put.</summary>
            private readonly float2 Loiter(Entity entity, quaternion rotation, float maxStep, ref float2 heading)
            {
                if (!Flights.TryGetComponent(entity, out var flight) || flight.LoiterRadius <= 0f || IsLanded(entity))
                {
                    return float2.zero;
                }

                heading = FlightMath.Loiter(rotation, maxStep, flight.LoiterRadius);
                return heading * maxStep;
            }

            private readonly bool IsLanded(Entity entity) => GivingWay.IsLanded(entity);

            private readonly float2 Separation(ref SeparationVisitor crowd, float maxStep)
            {
                Index.QueryHulls(crowd.Center, crowd.Reach, ref crowd);
                var length = math.length(crowd.Push);
                return length > maxStep ? crowd.Push * (maxStep / length) : crowd.Push;
            }

            /// <summary>This frame's step and facing before the crowd and walls have their say.</summary>
            private readonly float2 Plan(Entity entity, in LocalTransform transform, float topSpeed, in NavAgent agent,
                float3 goal, EnabledRefRW<MoveDestination> moving, ref PathState path,
                DynamicBuffer<PathWaypoint> waypoints, in Locomotion locomotion, ref LocomotionState drive,
                ref float2 heading)
            {
                var maxStep = topSpeed * DeltaTime;
                var facing = math.normalize(math.mul(transform.Rotation, math.forward()).xz);
                var leg = default(PathLeg);
                if (moving.ValueRO && !Follow(transform.Position, facing, agent, maxStep, locomotion, drive.Speed, goal,
                        ref path, waypoints, out leg))
                {
                    Halt(moving, ref path, waypoints);
                }

                if (moving.ValueRO)
                {
                    var step = locomotion.Drive(ref drive, transform.Position.xz, ref facing, leg, topSpeed,
                        agent.Radius, DeltaTime);
                    heading = facing;
                    return step;
                }

                return agent.Layer == NavLayer.Air
                    ? Loiter(entity, transform.Rotation, maxStep, ref heading)
                    : locomotion.BrakeToStop(ref drive, facing, DeltaTime);
            }

            /// <summary>Picks the leg to drive; false once the unit has arrived.</summary>
            private readonly bool Follow(float3 position, float2 facing, in NavAgent agent, float maxStep,
                in Locomotion locomotion, float speed, float3 goal, ref PathState path,
                DynamicBuffer<PathWaypoint> waypoints, out PathLeg leg)
            {
                var reach = locomotion.LookAhead(speed, agent.Radius);
                path.SkipCorners(Grid, agent, position, math.max(agent.Radius * 0.5f, maxStep), reach, waypoints);
                // No corners yet (search pending): head straight for the goal meanwhile.
                leg = PathLeg.Along(position.xz, goal.xz, waypoints, path);
                return !locomotion.HasArrived(leg, position.xz, facing, agent.Radius);
            }
        }
    }
}
