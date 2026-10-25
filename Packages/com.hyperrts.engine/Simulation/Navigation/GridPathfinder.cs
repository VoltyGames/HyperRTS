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
        /// Fills the waypoints; false when the goal was snapped. A goal outside the start's region snaps to the nearest
        /// cell inside it, or the unit stays where it is. Adds the cells it looked at to the work.
        /// </summary>
        public static bool Plan(ref GridAStar search, in NavAccess access, float3 start, float3 goal,
            DynamicBuffer<PathWaypoint> waypoints, ref int work)
        {
            waypoints.Clear();
            var grid = access.Grid;
            var startCell = grid.WorldToCell(start);
            if (!access.TryFindNearest(startCell, NearestSearchRadius, out var from))
            {
                Add(waypoints, goal, start.y);
                return false;
            }

            // A unit too near a wall (a building placed on it) first steps out to open ground.
            var origin = start;
            if (!from.Equals(startCell))
            {
                origin = grid.CellCenter(from);
                Add(waypoints, origin, start.y);
            }

            var goalCell = grid.WorldToCell(goal);
            if (!access.TryFindNearest(goalCell, ReachableSearchRadius, access.Region(from), out var to))
            {
                Stay(waypoints, origin, start.y);
                return false;
            }

            var exact = to.Equals(goalCell);
            var end = exact ? goal : grid.CellCenter(to);
            if (!Route(ref search, access, from, to, origin, end, waypoints, start.y, ref work))
            {
                Stay(waypoints, origin, start.y);
                return false;
            }

            return exact;
        }

        // Straight when in sight, else A* and string pulling; false only when regions and the search disagree.
        private static bool Route(ref GridAStar search, in NavAccess access, int2 from, int2 to, float3 origin,
            float3 end, DynamicBuffer<PathWaypoint> waypoints, float y, ref int work)
        {
            work += (int)(math.distance(origin.xz, end.xz) / access.Grid.CellSize) + 1;
            if (from.Equals(to) || access.HasLineOfSight(origin, end))
            {
                Add(waypoints, end, y);
                return true;
            }

            var cells = new NativeList<int2>(64, Allocator.Temp);
            var reached = search.Search(access, from, to, cells, out var expansions);
            work += expansions;
            if (reached)
            {
                Smooth(access, origin, end, cells, waypoints, y);
            }

            cells.Dispose();
            return reached;
        }

        // Greedy string pulling: keep a corner only when the last kept point can't see the point after it.
        private static void Smooth(in NavAccess access, float3 origin, float3 end, NativeList<int2> cells,
            DynamicBuffer<PathWaypoint> waypoints, float y)
        {
            var anchor = origin;
            for (var i = 1; i < cells.Length - 1; i++)
            {
                var point = access.Grid.CellCenter(cells[i]);
                var next = i + 1 == cells.Length - 1 ? end : access.Grid.CellCenter(cells[i + 1]);
                if (!access.HasLineOfSight(anchor, next))
                {
                    Add(waypoints, point, y);
                    anchor = point;
                }
            }

            Add(waypoints, end, y);
        }

        private static void Stay(DynamicBuffer<PathWaypoint> waypoints, float3 origin, float y)
        {
            if (waypoints.Length == 0)
            {
                Add(waypoints, origin, y);
            }
        }

        private static void Add(DynamicBuffer<PathWaypoint> waypoints, float3 point, float y) =>
            waypoints.Add(new PathWaypoint { Position = new float3(point.x, y, point.z) });
    }
}
