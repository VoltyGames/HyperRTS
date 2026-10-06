using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Spatial;
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
    /// pushes overlapping units of the same layer apart and never steps into a cell closed to the agent's layer.
    /// Steers on XZ; with a <see cref="NavGrid"/>, Y follows the layer's surface (ground, deck or water). Aircraft
    /// ignore the grid, climb to their <see cref="Flight"/> altitude (land while <see cref="Docked"/>) and, if they
    /// can't hover, circle when idle.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    public partial struct MovementSystem : ISystem
    {
        private NavGrid _noGrid;
        private ComponentLookup<Flight> _flights;
        private ComponentLookup<Docked> _docked;

        public void OnCreate(ref SystemState state)
        {
            _noGrid = NavGrid.Placeholder();
            _flights = state.GetComponentLookup<Flight>(true);
            _docked = state.GetComponentLookup<Docked>(true);
            state.RequireForUpdate<SpatialIndex>();
        }

        public void OnDestroy(ref SystemState state) => _noGrid.Dispose();

        /// <summary>Distance from the final waypoint that counts as arrived.</summary>
        private static float ArriveTolerance(float radius) => math.max(0.05f, radius * 0.25f);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hasGrid = SystemAPI.TryGetSingleton<NavGrid>(out var grid) && grid.IsCreated;
            _flights.Update(ref state);
            _docked.Update(ref state);
            new MoveJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Grid = hasGrid ? grid : _noGrid,
                HasGrid = hasGrid,
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
            public bool HasGrid;
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
                var wanted = from + step + Separation(entity, transform.Position, agent, maxStep);
                if (flying)
                {
                    Fly(entity, ref transform, wanted);
                }
                else
                {
                    Walk(ref transform, from, wanted, agent, moving.ValueRO, waypoints, ref path);
                }

                if (math.any(heading != 0f))
                {
                    transform.Rotation = quaternion.LookRotationSafe(new float3(heading.x, 0f, heading.y), math.up());
                }
            }

            private readonly void Walk(ref LocalTransform transform, float2 from, float2 wanted, in NavAgent agent,
                bool moving, DynamicBuffer<PathWaypoint> waypoints, ref PathState path)
            {
                transform.Position.xz = Constrain(from, wanted, NavLayers.Surfaces(agent.Layer));
                if (HasGrid)
                {
                    transform.Position.y = Grid.HeightFor(transform.Position, agent.Layer);
                }

                // Shoved off its line by the crowd, the unit may now face a wall between it and the next corner.
                var shoved = !transform.Position.xz.Equals(wanted);
                if (!moving || !shoved || waypoints.Length == 0)
                {
                    return;
                }

                if (!Grid.HasLineOfSight(transform.Position, waypoints[0].Position, 0f, agent.Layer))
                {
                    path.Status = PathStatus.None;
                }
            }

            /// <summary>Aircraft go wherever they steer and climb toward their altitude over the surface below.</summary>
            private readonly void Fly(Entity entity, ref LocalTransform transform, float2 wanted)
            {
                transform.Position.xz = wanted;
                var surface = HasGrid ? Grid.SurfaceHeight(transform.Position) : 0f;
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

            private readonly bool IsLanded(Entity entity) => Docked.Of(DockedLookup, entity);

            private readonly float2 Separation(Entity entity, float3 position, in NavAgent agent, float maxStep)
            {
                var separation = new SeparationVisitor
                {
                    Self = entity, Position = position.xz, Radius = agent.Radius, Layer = agent.Layer,
                };
                Index.Query(position, agent.Radius, ref separation);
                var length = math.length(separation.Push);
                return length > maxStep ? separation.Push * (maxStep / length) : separation.Push;
            }

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
                if (waypoints.Length <= 1 && distance <= ArriveTolerance(agent.Radius))
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

                if (!HasGrid || distance > MoveOrderSystem.StallRadius(agent.Radius))
                {
                    return false;
                }

                return Grid.HasLineOfSight(position, waypoints[1].Position, Grid.Clearance(agent.Radius), agent.Layer);
            }

            /// <summary>Rejects or slides a move that would enter a closed cell; units already inside may leave.</summary>
            private readonly float2 Constrain(float2 from, float2 to, NavSurface surfaces)
            {
                if (!HasGrid || IsOpen(to, surfaces) || !IsOpen(from, surfaces))
                {
                    return to;
                }

                if (IsOpen(new float2(to.x, from.y), surfaces))
                {
                    return new float2(to.x, from.y);
                }

                return IsOpen(new float2(from.x, to.y), surfaces) ? new float2(from.x, to.y) : from;
            }

            private readonly bool IsOpen(float2 position, NavSurface surfaces) =>
                Grid.IsOpen(Grid.WorldToCell(position), surfaces);
        }
    }
}
