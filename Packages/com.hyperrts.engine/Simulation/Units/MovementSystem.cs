using HyperRTS.Core;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Follows <see cref="PathWaypoint"/>s toward an enabled <see cref="MoveDestination"/> (disabled on arrival),
    /// pushes overlapping units apart and never steps into a blocked <see cref="NavGrid"/> cell. XZ only.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    public partial struct MovementSystem : ISystem
    {
        // Jobs reject unallocated containers, so a world without a map hands the job this unused stand-in.
        private NativeArray<byte> _noCells;

        public void OnCreate(ref SystemState state)
        {
            _noCells = new NativeArray<byte>(1, Allocator.Persistent);
            state.RequireForUpdate<SpatialIndex>();
        }

        public void OnDestroy(ref SystemState state) => _noCells.Dispose();

        /// <summary>Distance from the final waypoint that counts as arrived.</summary>
        private static float ArriveTolerance(float radius) => math.max(0.05f, radius * 0.25f);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hasGrid = SystemAPI.TryGetSingleton<NavGrid>(out var grid) && grid.IsCreated;
            new MoveJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Grid = hasGrid ? grid : new NavGrid { Cells = _noCells },
                HasGrid = hasGrid,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(MoveDestination))]
        private partial struct MoveJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            [ReadOnly] public NavGrid Grid;
            public bool HasGrid;
            public float DeltaTime;

            // `ref`, not `in`: it must share one writable handle with EnabledRefRW.
            private void Execute(Entity entity, ref LocalTransform transform, in MovementSpeed speed, in NavAgent agent,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, ref PathState path,
                DynamicBuffer<PathWaypoint> waypoints)
            {
                var maxStep = speed.Value * DeltaTime;
                var heading = float2.zero;
                var step = float2.zero;
                if (moving.ValueRO && !Steer(transform.Position, agent.Radius, maxStep, destination.Value, waypoints,
                        ref heading, ref step))
                {
                    moving.ValueRW = false;
                    path.Status = PathStatus.None;
                    waypoints.Clear();
                }

                var from = transform.Position.xz;
                var wanted = from + step + Separation(entity, transform.Position, agent.Radius, maxStep);
                transform.Position.xz = Constrain(from, wanted);
                if (math.any(heading != 0f))
                {
                    transform.Rotation = quaternion.LookRotationSafe(new float3(heading.x, 0f, heading.y), math.up());
                }

                // Shoved off its line by the crowd, the unit may now face a wall between it and the next corner.
                if (moving.ValueRO && !transform.Position.xz.Equals(wanted) && waypoints.Length > 0 &&
                    !Grid.HasLineOfSight(transform.Position, waypoints[0].Position, 0f))
                {
                    path.Status = PathStatus.None;
                }
            }

            private readonly float2 Separation(Entity entity, float3 position, float radius, float maxStep)
            {
                var separation = new SeparationVisitor { Self = entity, Position = position.xz, Radius = radius };
                Index.Query(position, radius, ref separation);
                var length = math.length(separation.Push);
                return length > maxStep ? separation.Push * (maxStep / length) : separation.Push;
            }

            /// <summary>Steps toward the next corner; false once the final one is reached.</summary>
            private readonly bool Steer(float3 position, float radius, float maxStep, float3 goal,
                DynamicBuffer<PathWaypoint> waypoints, ref float2 heading, ref float2 step)
            {
                while (waypoints.Length > 1 && CanSkipCorner(position, radius, maxStep, waypoints))
                {
                    waypoints.RemoveAt(0);
                }

                // No corners yet (search pending): head straight for the goal meanwhile.
                var target = waypoints.Length > 0 ? waypoints[0].Position.xz : goal.xz;
                var toTarget = target - position.xz;
                var distance = math.length(toTarget);
                if (waypoints.Length <= 1 && distance <= ArriveTolerance(radius))
                {
                    return false;
                }

                heading = toTarget / distance;
                step = heading * math.min(distance, maxStep);
                return true;
            }

            // A crowd can't all touch the same corner, so near one, skip it once the next corner is in sight.
            private readonly bool CanSkipCorner(float3 position, float radius, float maxStep,
                DynamicBuffer<PathWaypoint> waypoints)
            {
                var distance = math.distance(position.xz, waypoints[0].Position.xz);
                if (distance <= math.max(radius * 0.5f, maxStep))
                {
                    return true;
                }

                return HasGrid && distance <= MoveOrderSystem.StallRadius(radius) &&
                       Grid.HasLineOfSight(position, waypoints[1].Position, Grid.Clearance(radius));
            }

            /// <summary>Rejects or slides a move that would enter a blocked cell; units already inside may leave.</summary>
            private readonly float2 Constrain(float2 from, float2 to)
            {
                if (!HasGrid || IsOpen(to) || !IsOpen(from))
                {
                    return to;
                }

                if (IsOpen(new float2(to.x, from.y)))
                {
                    return new float2(to.x, from.y);
                }

                return IsOpen(new float2(from.x, to.y)) ? new float2(from.x, to.y) : from;
            }

            private readonly bool IsOpen(float2 position) => Grid.IsWalkable(Grid.WorldToCell(position));
        }
    }
}
