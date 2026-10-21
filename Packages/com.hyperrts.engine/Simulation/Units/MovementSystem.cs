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
    /// footprint into cells closed to the agent (<see cref="NavCapsule.Constrain"/>).
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

        public void OnCreate(ref SystemState state)
        {
            _flights = state.GetComponentLookup<Flight>(true);
            _docked = state.GetComponentLookup<Docked>(true);
            _blockers = SystemAPI.QueryBuilder().WithPresent<Blocker>().Build();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _flights.Update(ref state);
            _docked.Update(ref state);
            new MoveJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Grid = SystemAPI.GetSingleton<NavGrid>(),
                // Asked of the query, not the index, which its builder is still filling.
                HasBlockers = !_blockers.IsEmptyIgnoreFilter,
                Flights = _flights,
                DockedLookup = _docked,
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
            public float DeltaTime;

            // `ref`, not `in`: it must share one writable handle with EnabledRefRW.
            private void Execute(Entity entity, ref LocalTransform transform, in MovementSpeed speed, in NavAgent agent,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, ref PathState path,
                DynamicBuffer<PathWaypoint> waypoints)
            {
                var maxStep = speed.Value * DeltaTime;
                var heading = float2.zero;
                var step = float2.zero;
                if (moving.ValueRO && !Steer(transform.Position, agent, maxStep, destination.Value, waypoints,
                        ref heading, ref step))
                {
                    moving.ValueRW = false;
                    path.Status = PathStatus.None;
                    waypoints.Clear();
                }

                var flying = agent.Layer == NavLayer.Air;
                if (flying && !moving.ValueRO)
                {
                    step = Loiter(entity, transform.Rotation, maxStep, ref heading);
                }

                var from = transform.Position.xz;
                var axis = agent.SegmentAxis(transform.Rotation);
                var wanted = from + step + Separation(Crowd(entity, from, axis, agent), agent, maxStep);
                if (flying)
                {
                    Fly(entity, ref transform, wanted);
                }
                else
                {
                    if (HasBlockers)
                    {
                        wanted += Clearance(Crowd(entity, wanted, axis, agent), agent);
                    }

                    Walk(ref transform, from, wanted, agent, axis, moving.ValueRO, waypoints, ref path);
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

            private readonly float2 Separation(SeparationVisitor separation, in NavAgent agent, float maxStep)
            {
                Index.QueryHulls(Center(separation), Reach(agent), ref separation);
                var length = math.length(separation.Push);
                return length > maxStep ? separation.Push * (maxStep / length) : separation.Push;
            }

            // Measured where the unit is headed and left unclamped, so it never sinks into a blocker even for a frame.
            private readonly float2 Clearance(SeparationVisitor clearance, in NavAgent agent)
            {
                Index.QueryBlockers(Center(clearance), Reach(agent), ref clearance);
                return clearance.Push;
            }

            private static SeparationVisitor Crowd(Entity entity, float2 position, float2 axis, in NavAgent agent) =>
                new()
                {
                    Self = entity,
                    Position = position,
                    Radius = agent.Radius,
                    SegmentAxis = axis,
                    Spacing = agent.Spacing,
                    Layer = agent.Layer,
                };

            // Queries only read XZ.
            private static float3 Center(in SeparationVisitor visitor) =>
                new(visitor.Position.x, 0f, visitor.Position.y);

            private static float Reach(in NavAgent agent) => agent.HalfLength + agent.Spacing;

            /// <summary>Steps toward the next corner; false once the final one is reached.</summary>
            private readonly bool Steer(float3 position, in NavAgent agent, float maxStep, float3 goal,
                DynamicBuffer<PathWaypoint> waypoints, ref float2 heading, ref float2 step)
            {
                while (waypoints.Length > 1 && CanSkipCorner(position, agent, maxStep, waypoints))
                {
                    waypoints.RemoveAt(0);
                }

                // No corners yet (search pending): head straight for the goal meanwhile.
                var target = waypoints.Length > 0 ? waypoints[0].Position.xz : goal.xz;
                var toTarget = target - position.xz;
                var distance = math.length(toTarget);
                if (waypoints.Length <= 1 && distance <= NavTolerances.Arrive(agent.Radius))
                {
                    return false;
                }

                heading = toTarget / distance;
                step = heading * math.min(distance, maxStep);
                return true;
            }

            // A crowd can't all touch the same corner, so near one, skip it once the next corner is in sight.
            private readonly bool CanSkipCorner(float3 position, in NavAgent agent, float maxStep,
                DynamicBuffer<PathWaypoint> waypoints)
            {
                var distance = math.distance(position.xz, waypoints[0].Position.xz);
                if (distance <= math.max(agent.Radius * 0.5f, maxStep))
                {
                    return true;
                }

                if (distance > NavTolerances.Crowded(agent.Radius))
                {
                    return false;
                }

                return agent.Pass(Grid).HasLineOfSight(position, waypoints[1].Position, true);
            }
        }
    }
}
