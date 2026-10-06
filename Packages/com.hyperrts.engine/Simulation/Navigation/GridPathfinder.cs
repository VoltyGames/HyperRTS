using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Turns a start and goal into smoothed <see cref="PathWaypoint"/>s over a <see cref="NavGrid"/>.</summary>
    internal static class GridPathfinder
    {
        /// <summary>How far (in cells) to look for open ground around a blocked start or goal.</summary>
        public const int NearestSearchRadius = 32;

        public static void Plan(in NavGrid grid, in PathRequest request, DynamicBuffer<PathWaypoint> waypoints)
        {
            waypoints.Clear();
            var start = request.Start;
            var goal = request.Goal;
            var layer = request.Layer;
            var startCell = grid.WorldToCell(start);
            var goalCell = grid.WorldToCell(goal);
            var foundStart = grid.TryFindNearestWalkable(startCell, NearestSearchRadius, out var from, layer);
            var foundGoal = grid.TryFindNearestWalkable(goalCell, NearestSearchRadius, out var to, layer);
            if (!foundStart || !foundGoal)
            {
                Add(waypoints, goal, start.y);
                return;
            }

            // A unit inside a blocked cell (a building placed on it) first steps out to open ground.
            var origin = start;
            if (!from.Equals(startCell))
            {
                origin = grid.CellCenter(from);
                Add(waypoints, origin, start.y);
            }

            var end = to.Equals(goalCell) ? goal : grid.CellCenter(to);
            var clearance = grid.Clearance(request.Radius);
            if (from.Equals(to) || grid.HasLineOfSight(origin, end, clearance, layer))
            {
                Add(waypoints, end, start.y);
                return;
            }

            var cells = new NativeList<int2>(64, Allocator.Temp);
            if (!GridAStar.Search(grid, layer, from, to, cells))
            {
                end = grid.CellCenter(cells[cells.Length - 1]);
            }

            Smooth(grid, layer, origin, end, clearance, cells, waypoints, start.y);
            cells.Dispose();
        }

        // Greedy string pulling: keep a corner only when the anchor can't see the point after it.
        private static void Smooth(in NavGrid grid, NavLayer layer, float3 origin, float3 end, float clearance,
            NativeList<int2> cells, DynamicBuffer<PathWaypoint> waypoints, float y)
        {
            var anchor = origin;
            for (var i = 1; i < cells.Length - 1; i++)
            {
                var point = grid.CellCenter(cells[i]);
                var next = i + 1 == cells.Length - 1 ? end : grid.CellCenter(cells[i + 1]);
                if (!grid.HasLineOfSight(anchor, next, clearance, layer))
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
