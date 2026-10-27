using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Planning state of a unit's path.</summary>
    public enum PathStatus : byte
    {
        /// <summary>No path; the next enabled destination requests one.</summary>
        None = 0,

        /// <summary>Waiting for a pathfinding slot; a unit with no corners yet brakes and turns to its goal meanwhile.</summary>
        Requested = 1,
        Ready = 2,
    }

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

        /// <summary>Walls held the unit when it asked for a replan (<see cref="PathRequest.Stuck"/>).</summary>
        public bool Stuck;

        /// <summary>
        /// The drive budget ran out before its later stretches, left as drawn; they are planned once the planned part
        /// is driven.
        /// </summary>
        public bool PartlyPlanned;

        /// <summary>Whether an <see cref="PartlyPlanned"/> path has driven its planned part.</summary>
        public readonly bool IsRestDue(DynamicBuffer<PathWaypoint> waypoints) =>
            PartlyPlanned && (waypoints.Length == 0 || !waypoints[0].Tight);

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
        /// Passes corners within <paramref name="touch"/>, and nearer ones once the next is in sight: a crowd can't all
        /// touch the same corner and a vehicle cuts it. A <see cref="PathWaypoint.Tight"/> one is never cut: it passes
        /// once touched, or once behind the unit where the gear doesn't change.
        /// </summary>
        public void SkipCorners(in NavGrid grid, in NavAgent agent, float3 position, float touch, float reach,
            DynamicBuffer<PathWaypoint> waypoints)
        {
            while (waypoints.Length > 1 && CanSkipCorner(grid, agent, position, touch, reach, waypoints))
            {
                PassCorner(waypoints);
            }
        }

        private static bool CanSkipCorner(in NavGrid grid, in NavAgent agent, float3 position, float touch,
            float reach, DynamicBuffer<PathWaypoint> waypoints)
        {
            var distance = math.distance(position.xz, waypoints[0].Position.xz);
            if (waypoints[0].Tight)
            {
                return IsPassed(position.xz, distance, touch, agent.Radius, waypoints[0], waypoints[1]);
            }

            if (distance <= touch)
            {
                return true;
            }

            if (distance > math.max(CornerSkipRange(agent.Radius), reach))
            {
                return false;
            }

            return agent.Access(grid).HasLineOfSight(position, waypoints[1].Position);
        }

        // A gear change is passed on arriving or overshooting it, where the next leg doubles back.
        private static bool IsPassed(float2 position, float distance, float touch, float radius,
            in PathWaypoint corner, in PathWaypoint next)
        {
            var ahead = next.Position.xz - corner.Position.xz;
            var beyond = math.dot(position - corner.Position.xz, ahead);
            if (corner.Reverse != next.Reverse)
            {
                return distance <= NavTolerances.Arrive(radius) || beyond < 0f;
            }

            return distance <= touch || beyond >= 0f;
        }

        // Only corners this near are skipped, so a unit keeps to its smoothed path rather than cutting across.
        private static float CornerSkipRange(float radius) => radius * 4f + 1f;

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
            Stuck = false;
            ClearWaypoints(waypoints);
        }

        /// <summary>
        /// Plans the whole path again, keeping its corners to drive on while the search waits, and records whether
        /// walls hold the unit.
        /// </summary>
        public void Replan(bool stuck)
        {
            Status = PathStatus.None;
            Cause = PathCause.Replan;
            Stuck = stuck;
        }

        public void ClearWaypoints(DynamicBuffer<PathWaypoint> waypoints)
        {
            waypoints.Clear();
            LengthPastCorner = 0f;
        }

        /// <summary>
        /// Re-aims the path's end at a moved goal in sight of the corner before it, or of the unit on the last leg.
        /// False when it needs a search: out of sight, moved over a quarter of the path left, which may have opened a
        /// shorter way, or the end of a planned manoeuvre.
        /// </summary>
        public bool TryFollowGoal(in NavAccess access, float3 position, float3 goal,
            DynamicBuffer<PathWaypoint> waypoints)
        {
            var last = waypoints.Length - 1;
            if (last < 0 || waypoints[last].Tight)
            {
                return false;
            }

            var left = math.distance(position.xz, waypoints[0].Position.xz) + LengthPastCorner;
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
