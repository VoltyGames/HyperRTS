using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>Which destination and grid version a unit's <see cref="PathWaypoint"/>s were planned for.</summary>
    public struct PathState : IComponentData
    {
        /// <summary>Fewest seconds between a chasing unit's searches; between them it re-aims the path's end by sight.</summary>
        public const float ChaseSearchSeconds = 0.5f;

        private const float ChaseDriftShare = 0.25f;

        public float3 Goal;
        public int GridVersion;
        public PathStatus Status;

        /// <summary>Frame of the pending request; older requests are served first.</summary>
        public uint RequestFrame;

        /// <summary>Why the path was dropped, then why the pending request was made.</summary>
        public PathCause Cause;

        /// <summary>Elapsed seconds when the path was planned.</summary>
        public double PlanTime;

        /// <summary>The goal was snapped to ground the unit can reach, so any grid change re-plans it in case the way opened.</summary>
        public bool IsShort;

        /// <summary>Path length from the first <see cref="PathWaypoint"/> to the last, kept as corners change.</summary>
        public float LengthPastCorner;

        /// <summary>Path length from the first waypoint to the last.</summary>
        public static float Measure(DynamicBuffer<PathWaypoint> waypoints)
        {
            var length = 0f;
            for (var i = 1; i < waypoints.Length; i++)
            {
                length += math.distance(waypoints[i - 1].Position.xz, waypoints[i].Position.xz);
            }

            return length;
        }

        /// <summary>
        /// Passes corners within <paramref name="touch"/>, and nearer ones once the next is in sight, so a crowd
        /// doesn't queue at one corner.
        /// </summary>
        public void SkipCorners(in NavAccess access, float2 position, float touch, float reach,
            DynamicBuffer<PathWaypoint> waypoints)
        {
            while (waypoints.Length > 1 && CanSkipCorner(access, position, touch, reach, waypoints))
            {
                PassCorner(waypoints);
            }
        }

        private static bool CanSkipCorner(in NavAccess access, float2 position, float touch, float reach,
            DynamicBuffer<PathWaypoint> waypoints)
        {
            var radius = access.Class.Radius;
            var distance = math.distance(position, waypoints[0].Position.xz);
            if (distance <= touch)
            {
                return true;
            }

            if (distance > math.max(CornerSkipRange(radius), reach))
            {
                return false;
            }

            // Line of sight reads only XZ.
            return access.HasLineOfSight(new float3(position.x, 0f, position.y), waypoints[1].Position);
        }

        // Only corners this near are skipped, so a unit keeps to its smoothed path rather than cutting across.
        private static float CornerSkipRange(float radius) => radius * 4f + 1f;

        /// <summary>The path length from <paramref name="position"/> through the waypoints, which must not be empty.</summary>
        public readonly float Remaining(float2 position, DynamicBuffer<PathWaypoint> waypoints) =>
            math.distance(position, waypoints[0].Position.xz) + LengthPastCorner;

        /// <summary>Drops the first waypoint, keeping <see cref="LengthPastCorner"/> in step.</summary>
        public void PassCorner(DynamicBuffer<PathWaypoint> waypoints)
        {
            LengthPastCorner = math.max(0f,
                LengthPastCorner - math.distance(waypoints[0].Position.xz, waypoints[1].Position.xz));
            waypoints.RemoveAt(0);
        }

        /// <summary>Ends the move: disables the destination and drops the path.</summary>
        public void Halt(EnabledRefRW<MoveDestination> moving, DynamicBuffer<PathWaypoint> waypoints)
        {
            moving.ValueRW = false;
            Drop(waypoints);
        }

        /// <summary>Drops the path and its corners, so the next move plans afresh.</summary>
        public void Drop(DynamicBuffer<PathWaypoint> waypoints)
        {
            Status = PathStatus.None;
            Cause = PathCause.Order;
            ClearWaypoints(waypoints);
        }

        /// <summary>Plans the whole path again, keeping its corners to drive on while the search waits.</summary>
        public void Replan()
        {
            Status = PathStatus.None;
            Cause = PathCause.Replan;
        }

        /// <summary>
        /// Drops the corners when <paramref name="dropsCorners"/> and asks for a search, unless one is pending:
        /// records <paramref name="cause"/> and the frame.
        /// </summary>
        public void Request(DynamicBuffer<PathWaypoint> waypoints, PathCause cause, bool dropsCorners, uint frame)
        {
            if (dropsCorners)
            {
                ClearWaypoints(waypoints);
            }

            if (Status == PathStatus.Requested)
            {
                return;
            }

            Cause = cause;
            Status = PathStatus.Requested;
            RequestFrame = frame;
        }

        public void ClearWaypoints(DynamicBuffer<PathWaypoint> waypoints)
        {
            waypoints.Clear();
            LengthPastCorner = 0f;
        }

        /// <summary>
        /// Re-aims the path's end at a moved goal in sight of the corner before it, or of the unit on the last leg.
        /// False when it needs a search: out of sight or moved over a quarter of the path left.
        /// </summary>
        public bool TryFollowGoal(in NavAccess access, float3 position, float3 goal,
            DynamicBuffer<PathWaypoint> waypoints)
        {
            var last = waypoints.Length - 1;
            if (last < 0)
            {
                return false;
            }

            var left = Remaining(position.xz, waypoints);
            var drift = math.distance(waypoints[last].Position.xz, goal.xz);
            if (last > 0 && drift > left * ChaseDriftShare)
            {
                return false;
            }

            var from = last > 0 ? waypoints[last - 1].Position : position;
            if (!access.HasLineOfSight(from, goal))
            {
                return false;
            }

            MoveLast(waypoints, goal.xz);
            Goal = goal;
            IsShort = false;
            return true;
        }

        /// <summary>Whether a chase may search again: its last search is <see cref="ChaseSearchSeconds"/> old.</summary>
        public readonly bool IsChaseSearchDue(double now) => now - PlanTime >= ChaseSearchSeconds;

        /// <summary>Whether the grid changed near enough to the path's legs to block one or open a cheaper way.</summary>
        public readonly bool IsNearChange(in NavGrid grid, DynamicBuffer<PathWaypoint> waypoints, float3 position,
            in NavAgent agent)
        {
            if (IsShort || !grid.Changes.Covers(GridVersion, grid.Version))
            {
                return true;
            }

            // A change past the wall margin can neither block a leg nor make another way cheaper.
            var margin = math.max(agent.Radius * 2f, agent.WallMargin) + grid.Grid.CellSize;
            var from = position.xz;
            for (var i = 0; i < waypoints.Length; i++)
            {
                var to = waypoints[i].Position.xz;
                if (grid.Changes.Touches(grid, GridVersion, from, to, margin))
                {
                    return true;
                }

                from = to;
            }

            return false;
        }

        /// <summary>Moves the path's end onto a goal nudged within a cell.</summary>
        public void FollowSmallGoalMove(in NavGrid grid, DynamicBuffer<PathWaypoint> waypoints, float3 goal,
            NavLayer layer)
        {
            // Re-planning for sub-cell nudges (re-clicks, a creeping chase target) isn't worth a search.
            if (waypoints.Length == 0 || !grid.IsWalkable(goal, layer))
            {
                return;
            }

            var last = waypoints[waypoints.Length - 1];
            var distanceSq = math.distancesq(last.Position.xz, goal.xz);
            if (distanceSq <= 1e-6f || distanceSq > grid.Grid.CellSize * grid.Grid.CellSize)
            {
                return;
            }

            MoveLast(waypoints, goal.xz);
        }

        /// <summary>Moves the last waypoint, keeping <see cref="LengthPastCorner"/> in step.</summary>
        public void MoveLast(DynamicBuffer<PathWaypoint> waypoints, float2 position)
        {
            var last = waypoints.Length - 1;
            if (last > 0)
            {
                var before = waypoints[last - 1].Position.xz;
                LengthPastCorner += math.distance(before, position) - math.distance(before, waypoints[last].Position.xz);
            }

            ref var point = ref waypoints.ElementAt(last);
            point.Position.xz = position;
        }
    }
}
