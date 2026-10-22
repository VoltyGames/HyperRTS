using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Turns a start and goal into smoothed <see cref="PathWaypoint"/>s over a <see cref="NavAccess"/>.</summary>
    internal static class GridPathfinder
    {
        /// <summary>How far (in cells) to look for open ground around a blocked start.</summary>
        public const int NearestSearchRadius = 32;

        /// <summary>How far (in cells) to look round a goal for ground the start's region reaches.</summary>
        public const int ReachableSearchRadius = 64;

        /// <summary>
        /// Re-plans only the way to the next corner and keeps the corners after it, so a shoved unit goes on round
        /// the side it was taking. A corner it can no longer reach gets a whole new path. False when it ends short.
        /// </summary>
        public static bool Repair(ref GridAStar search, in NavAccess access, in PathRequest request,
            DynamicBuffer<PathWaypoint> waypoints, ref int work)
        {
            var corner = waypoints[0].Position;
            var rest = new NativeList<PathWaypoint>(waypoints.Length, Allocator.Temp);
            for (var i = 1; i < waypoints.Length; i++)
            {
                rest.Add(waypoints[i]);
            }

            var reached = Plan(ref search, access, request.Start, corner, waypoints, ref work);
            if (reached)
            {
                waypoints.AddRange(rest.AsArray());
            }
            else
            {
                reached = Plan(ref search, access, request.Start, request.Goal, waypoints, ref work);
            }

            rest.Dispose();
            return reached;
        }

        /// <summary>
        /// Fills the waypoints; false when they end short of the goal. A goal the start's region can't reach snaps to
        /// the nearest ground it can, or the unit stays where it is. Adds the cells it looked at to the work.
        /// </summary>
        public static bool Plan(ref GridAStar search, in NavAccess access, float3 start, float3 goal,
            DynamicBuffer<PathWaypoint> waypoints, ref int work)
        {
            waypoints.Clear();
            var startAnchor = access.AnchorAt(start);
            if (!access.TryFindNearest(startAnchor, NearestSearchRadius, out var from))
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

            var goalAnchor = access.AnchorAt(goal);
            if (!access.TryFindNearest(goalAnchor, ReachableSearchRadius, access.Region(from), out var to))
            {
                if (waypoints.Length == 0)
                {
                    Add(waypoints, origin, start.y);
                }

                return false;
            }

            var exact = to.Equals(goalAnchor);
            var end = exact ? goal : access.AnchorCenter(to);
            return Route(ref search, access, from, to, origin, end, waypoints, start.y, ref work) && exact;
        }

        // Straight when in sight, else A* and string pulling; false when the search ran out short of the end.
        private static bool Route(ref GridAStar search, in NavAccess access, int2 from, int2 to, float3 origin,
            float3 end, DynamicBuffer<PathWaypoint> waypoints, float y, ref int work)
        {
            work += (int)(math.distance(origin.xz, end.xz) / access.Grid.CellSize) + 1;
            if (from.Equals(to) || access.HasLineOfSight(origin, end, true))
            {
                Add(waypoints, end, y);
                return true;
            }

            var cells = new NativeList<int2>(64, Allocator.Temp);
            var reached = search.Search(access, from, to, NavGrid.MaxSearchExpansions, cells, out var expansions);
            work += expansions;
            if (!reached)
            {
                end = access.AnchorCenter(cells[cells.Length - 1]);
            }

            Smooth(access, origin, end, cells, waypoints, y);
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
