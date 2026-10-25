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
        public float3 Goal;
        public int GridVersion;
        public PathStatus Status;

        /// <summary>Frame of the pending request; older requests are served first.</summary>
        public uint RequestFrame;

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
        /// Passes corners within <paramref name="touch"/>, and nearer ones once the next is in sight: a crowd can't all
        /// touch the same corner and a vehicle cuts it.
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
            Status = PathStatus.None;
            ClearWaypoints(waypoints);
        }

        public void ClearWaypoints(DynamicBuffer<PathWaypoint> waypoints)
        {
            waypoints.Clear();
            LengthPastCorner = 0f;
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
