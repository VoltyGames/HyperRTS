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
    /// Follows <see cref="PathWaypoint"/>s toward an enabled <see cref="MoveDestination"/> (disabled on arrival),
    /// keeps units of the same layer their spacing apart, slides ground units round blockers and never pushes a
    /// footprint into cells closed to the agent (<see cref="NavCapsule.Constrain"/>). Units with
    /// <see cref="Locomotion"/> drive the path with its handling; the rest step straight at the next corner.
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
        /// <summary>Seconds a unit stays walled off or out of sight of its corner before it re-plans the way there.</summary>
        public const float ReplanDelay = 0.75f;

        private EntityQuery _blockers;
        private ComponentLookup<Flight> _flights;
        private ComponentLookup<Docked> _docked;
        private ComponentLookup<Locomotion> _locomotions;
        private ComponentLookup<LocomotionState> _locomotionStates;

        public void OnCreate(ref SystemState state)
        {
            _flights = state.GetComponentLookup<Flight>(true);
            _docked = state.GetComponentLookup<Docked>(true);
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
            _docked.Update(ref state);
            _locomotions.Update(ref state);
            _locomotionStates.Update(ref state);
            new MoveJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Grid = SystemAPI.GetSingleton<NavGrid>(),
                // Asked of the query, not the index, which its builder is still filling.
                HasBlockers = !_blockers.IsEmptyIgnoreFilter,
                Flights = _flights,
                DockedLookup = _docked,
                Locomotions = _locomotions,
                States = _locomotionStates,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Inside))]
        [WithPresent(typeof(MoveDestination))]
        private partial struct MoveJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            [ReadOnly] public NavGrid Grid;
            public bool HasBlockers;
            [ReadOnly] public ComponentLookup<Flight> Flights;
            [ReadOnly] public ComponentLookup<Docked> DockedLookup;
            [ReadOnly] public ComponentLookup<Locomotion> Locomotions;

            // Each unit writes only its own state.
            [NativeDisableParallelForRestriction] public ComponentLookup<LocomotionState> States;
            public float DeltaTime;

            // `ref`, not `in`: it must share one writable handle with EnabledRefRW.
            private void Execute(Entity entity, ref LocalTransform transform, in MovementSpeed speed, in NavAgent agent,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, ref PathState path,
                DynamicBuffer<PathWaypoint> waypoints)
            {
                var maxStep = speed.Value * DeltaTime;
                var heading = float2.zero;
                var driven = Locomotions.TryGetComponent(entity, out var locomotion);
                var drive = driven ? States[entity] : default;
                var step = Plan(entity, transform, speed.Value, agent, destination.Value, moving, ref path, waypoints,
                    locomotion, driven, ref drive, ref heading);

                var flying = agent.Layer == NavLayer.Air;
                var from = transform.Position.xz;
                var axis = agent.SegmentAxis(transform.Rotation);
                var wanted = from + step + Separation(new SeparationVisitor(entity, from, axis, agent), maxStep);
                if (flying)
                {
                    Fly(entity, ref transform, wanted);
                }
                else
                {
                    if (HasBlockers)
                    {
                        wanted += Clearance(new SeparationVisitor(entity, wanted, axis, agent));
                    }

                    Walk(ref transform, from, wanted, agent, axis, moving.ValueRO, waypoints, ref path);
                }

                if (driven)
                {
                    drive.Hit(transform.Position.xz - from, step, !transform.Position.xz.Equals(wanted));
                    States[entity] = drive;
                }

                var turned = quaternion.LookRotationSafe(new float3(heading.x, 0f, heading.y), math.up());
                if (math.any(heading != 0f) && (flying || CanTurn(transform, agent, axis, turned)))
                {
                    transform.Rotation = turned;
                }
            }

            private readonly void Walk(ref LocalTransform transform, float2 from, float2 wanted, in NavAgent agent,
                float2 axis, bool moving, DynamicBuffer<PathWaypoint> waypoints, ref PathState path)
            {
                var pass = agent.Pass(Grid);
                transform.Position.xz = new NavCapsule(pass, axis).Constrain(from, wanted);
                transform.Position.y = Grid.HeightFor(transform.Position, agent.Layer);
                var lost = moving && waypoints.Length > 0 &&
                           IsWalledOff(pass, from, wanted, transform.Position, waypoints[0].Position);
                path.Stuck = lost ? path.Stuck + DeltaTime : 0f;
                if (path.Stuck >= ReplanDelay)
                {
                    path.Status = PathStatus.None;
                    path.Repair = true;
                    path.Stuck = 0f;
                }
            }

            // Facing snaps to the heading, so a long hull mustn't swing an end into a closed cell; it moves on unturned.
            private readonly bool CanTurn(in LocalTransform transform, in NavAgent agent, float2 axis, quaternion turned)
            {
                if (agent.HalfSegment <= 0f)
                {
                    return true;
                }

                var capsule = new NavCapsule(agent.Pass(Grid), axis);
                return capsule.CanTurn(transform.Position.xz, agent.SegmentAxis(turned));
            }

            /// <summary>A wall stopped the move, and the unit either barely moved or lost sight of its next corner.</summary>
            private static bool IsWalledOff(in NavPass pass, float2 from, float2 wanted, float3 position, float3 corner)
            {
                if (position.xz.Equals(wanted))
                {
                    return false;
                }

                if (math.distancesq(from, position.xz) < math.distancesq(from, wanted) * 0.01f)
                {
                    return true;
                }

                return !pass.HasLineOfSight(position, corner, false);
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

            private readonly bool IsLanded(Entity entity) => DockedLookup.HasEnabled(entity);

            private readonly float2 Separation(SeparationVisitor separation, float maxStep)
            {
                Index.QueryHulls(separation.Center, separation.Reach, ref separation);
                var length = math.length(separation.Push);
                return length > maxStep ? separation.Push * (maxStep / length) : separation.Push;
            }

            // Measured where the unit is headed and left unclamped, so it never sinks into a blocker even for a frame.
            private readonly float2 Clearance(SeparationVisitor clearance)
            {
                Index.QueryBlockers(clearance.Center, clearance.Reach, ref clearance);
                return clearance.Push;
            }

            /// <summary>This frame's step and facing before the crowd and walls have their say.</summary>
            private readonly float2 Plan(Entity entity, in LocalTransform transform, float topSpeed, in NavAgent agent,
                float3 goal, EnabledRefRW<MoveDestination> moving, ref PathState path,
                DynamicBuffer<PathWaypoint> waypoints, in Locomotion locomotion, bool driven, ref LocomotionState drive,
                ref float2 heading)
            {
                var maxStep = topSpeed * DeltaTime;
                var position = transform.Position.xz;
                var facing = math.normalize(math.mul(transform.Rotation, math.forward()).xz);
                var leg = default(PathLeg);
                if (moving.ValueRO && !Follow(transform.Position, facing, agent, maxStep, locomotion, driven,
                        drive.Speed, goal, waypoints, out leg))
                {
                    moving.ValueRW = false;
                    path.Status = PathStatus.None;
                    waypoints.Clear();
                }

                if (driven && !moving.ValueRO)
                {
                    return locomotion.Coast(ref drive, facing, DeltaTime);
                }

                if (driven)
                {
                    var step = locomotion.Drive(ref drive, position, ref facing, leg, topSpeed, agent.Radius,
                        DeltaTime);
                    heading = facing;
                    return step;
                }

                if (agent.Layer == NavLayer.Air && !moving.ValueRO)
                {
                    return Loiter(entity, transform.Rotation, maxStep, ref heading);
                }

                heading = moving.ValueRO ? leg.Direction : float2.zero;
                return heading * math.min(leg.Distance, maxStep);
            }

            /// <summary>Picks the leg to drive; false once the unit has arrived.</summary>
            private readonly bool Follow(float3 position, float2 facing, in NavAgent agent, float maxStep,
                in Locomotion locomotion, bool driven, float speed, float3 goal, DynamicBuffer<PathWaypoint> waypoints,
                out PathLeg leg)
            {
                var reach = driven ? locomotion.LookAhead(speed, agent.Radius) : 0f;
                leg = Steer(position, agent, maxStep, reach, goal, waypoints);
                return driven
                    ? !locomotion.HasArrived(leg, position.xz, facing, agent.Radius)
                    : !leg.HasArrived(agent.Radius);
            }

            /// <summary>Skips the corners it may cut and returns the leg to the next one.</summary>
            private readonly PathLeg Steer(float3 position, in NavAgent agent, float maxStep, float reach, float3 goal,
                DynamicBuffer<PathWaypoint> waypoints)
            {
                while (waypoints.Length > 1 && CanSkipCorner(position, agent, maxStep, reach, waypoints))
                {
                    waypoints.RemoveAt(0);
                }

                // No corners yet (search pending): head straight for the goal meanwhile.
                return PathLeg.Along(position.xz, goal.xz, waypoints);
            }

            // A crowd can't all touch the same corner and a vehicle cuts it, so skip it once the next is in sight.
            private readonly bool CanSkipCorner(float3 position, in NavAgent agent, float maxStep, float reach,
                DynamicBuffer<PathWaypoint> waypoints)
            {
                var distance = math.distance(position.xz, waypoints[0].Position.xz);
                if (distance <= math.max(agent.Radius * 0.5f, maxStep))
                {
                    return true;
                }

                if (distance > math.max(NavTolerances.Crowded(agent.Radius), reach))
                {
                    return false;
                }

                return agent.Pass(Grid).HasLineOfSight(position, waypoints[1].Position, true);
            }
        }
    }
}
