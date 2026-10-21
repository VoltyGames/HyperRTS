using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Turns a start and goal into smoothed <see cref="PathWaypoint"/>s over a <see cref="NavAccess"/>.</summary>
    internal static class GridPathfinder
    {
        /// <summary>How far (in cells) to look for open ground around a blocked start or goal.</summary>
        public const int NearestSearchRadius = 32;

        /// <summary>
        /// Re-plans only the way to the next corner and keeps the corners after it, so a shoved unit goes on round
        /// the side it was taking. A corner it can no longer reach gets a whole new path.
        /// </summary>
        public static void Repair(in NavAccess access, in PathRequest request, DynamicBuffer<PathWaypoint> waypoints)
        {
            var corner = waypoints[0].Position;
            var rest = new NativeList<PathWaypoint>(waypoints.Length, Allocator.Temp);
            for (var i = 1; i < waypoints.Length; i++)
            {
                rest.Add(waypoints[i]);
            }

            if (Plan(access, request.Start, corner, waypoints))
            {
                waypoints.AddRange(rest.AsArray());
            }
            else
            {
                Plan(access, request.Start, request.Goal, waypoints);
            }

            rest.Dispose();
        }

        /// <summary>Fills the waypoints; false when the goal can't be reached and they end short of it.</summary>
        public static bool Plan(in NavAccess access, float3 start, float3 goal, DynamicBuffer<PathWaypoint> waypoints)
        {
            waypoints.Clear();
            var startAnchor = access.AnchorAt(start);
            var goalAnchor = access.AnchorAt(goal);
            var foundStart = access.TryFindNearest(startAnchor, NearestSearchRadius, out var from);
            if (!foundStart || !access.TryFindNearest(goalAnchor, NearestSearchRadius, out var to))
            {
                Add(waypoints, goal, start.y);
                return false;
            }

            // A unit inside a blocked cell (a building placed on it) first steps out to open ground.
            var origin = start;
            if (!from.Equals(startAnchor))
            {
                origin = access.AnchorCenter(from);
                Add(waypoints, origin, start.y);
            }

            var end = to.Equals(goalAnchor) ? goal : access.AnchorCenter(to);
            if (from.Equals(to) || access.HasLineOfSight(origin, end, true))
            {
                Add(waypoints, end, start.y);
                return true;
            }

            var cells = new NativeList<int2>(64, Allocator.Temp);
            var reached = GridAStar.Search(access, from, to, cells);
            if (!reached)
            {
                end = access.AnchorCenter(cells[cells.Length - 1]);
            }

            Smooth(access, origin, end, cells, waypoints, start.y);
            cells.Dispose();
            return reached;
        }

        // Greedy string pulling: keep a corner only when the anchor can't see the point after it.
        private static void Smooth(in NavAccess access, float3 origin, float3 end, NativeList<int2> cells,
            DynamicBuffer<PathWaypoint> waypoints, float y)
        {
            var anchor = origin;
            for (var i = 1; i < cells.Length - 1; i++)
            {
                var point = access.AnchorCenter(cells[i]);
                var next = i + 1 == cells.Length - 1 ? end : access.AnchorCenter(cells[i + 1]);
                if (!access.HasLineOfSight(anchor, next, true))
                {
                    Add(waypoints, point, y);
                    anchor = point;
                }
            }

            Add(waypoints, end, y);
        }

        private static void Add(DynamicBuffer<PathWaypoint> waypoints, float3 point, float y) =>
            waypoints.Add(new PathWaypoint { Position = new float3(point.x, y, point.z) });
    }
}
