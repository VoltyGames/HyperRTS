using System;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Turns a start and goal into smoothed path points over a <see cref="NavAccess"/>: snap both into the start's
    /// region, search the sector graph for a corridor, run the fine A* inside it, then pull the path tight.
    /// </summary>
    public static class GridPathfinder
    {
        /// <summary>How far (in cells) to look for open ground around a blocked start.</summary>
        public const int NearestSearchRadius = 32;

        /// <summary>How far (in cells) to look round a goal for ground the start's region reaches.</summary>
        public const int ReachableSearchRadius = 64;

        /// <summary>
        /// Snaps the start to open ground and the goal to the nearest cell in the start's region, and picks the route:
        /// straight when in sight, stay when no such cell is near, else a search.
        /// </summary>
        public static PathQuery Prepare(in NavAccess access, float3 start, float3 goal)
        {
            var grid = access.Grid;
            var startCell = grid.WorldToCell(start);
            if (!access.TryFindNearest(startCell, NearestSearchRadius, out var from))
            {
                return new PathQuery { Route = PathRoute.Straight, Origin = start, End = goal };
            }

            var query = new PathQuery { From = from, Origin = start, StepsOut = !from.Equals(startCell) };
            if (query.StepsOut)
            {
                query.Origin = grid.CellCenter(from);
            }

            var goalCell = grid.WorldToCell(goal);
            if (!access.TryFindNearest(goalCell, ReachableSearchRadius, access.Region(from), out var to))
            {
                query.Route = PathRoute.Stay;
                query.End = query.Origin;
                return query;
            }

            query.To = to;
            query.Exact = to.Equals(goalCell);
            query.End = query.Exact ? goal : grid.CellCenter(to);
            var straight = from.Equals(to) || access.HasLineOfSight(query.Origin, query.End);
            query.Route = straight ? PathRoute.Straight : PathRoute.Search;
            return query;
        }

        /// <summary>Adds the sectors of the abstract path, which the fine search's corridor is built round.</summary>
        public static void FindSectors(in NavAccess access, in PathQuery query, NativeList<int> sectors)
        {
            var search = new SectorSearch(Allocator.Temp);
            search.Find(access, query.From, query.To, sectors);
            search.Dispose();
        }

        /// <summary>
        /// The fine A* inside the abstract path's sectors and a ring round them, smoothed into the points; returns the
        /// cells expanded. The corridor's components connect start to goal by 4-connected steps A* allows, so it
        /// always finds the path.
        /// </summary>
        public static int Route(in NavAccess access, in PathQuery query, NativeArray<int> sectors,
            NativeList<float3> points)
        {
            var corridor = new NavCorridor(access.Grid.Field.SectorsPerAxis, Allocator.Temp);
            foreach (var sector in sectors)
            {
                corridor.AddWithRing(sector);
            }

            var search = new GridAStar(corridor.Cells, Allocator.Temp);
            var cells = new NativeList<int2>(256, Allocator.Temp);
            if (!search.Search(access, corridor, query.From, query.To, cells, out var expansions))
            {
                throw new InvalidOperationException("The fine search found no path inside its corridor.");
            }

            query.Begin(points);
            Smooth(access, query.Origin, query.End, cells, points);
            cells.Dispose();
            search.Dispose();
            corridor.Dispose();
            return expansions;
        }

        /// <summary>Every stage in turn; true when the path ends at the goal, not snapped.</summary>
        public static bool Plan(in NavAccess access, float3 start, float3 goal, NativeList<float3> points,
            out int expansions)
        {
            var query = Prepare(access, start, goal);
            expansions = 0;
            if (query.Route != PathRoute.Search)
            {
                query.AddDirect(points);
                return query.Exact;
            }

            expansions = Search(access, query, points);
            return query.Exact;
        }

        /// <summary>Finds the query's sector corridor and routes through it; returns the cells expanded.</summary>
        public static int Search(in NavAccess access, in PathQuery query, NativeList<float3> points)
        {
            using var sectors = new NativeList<int>(32, Allocator.Temp);
            FindSectors(access, query, sectors);
            return Route(access, query, sectors.AsArray(), points);
        }

        /// <summary>
        /// Greedy string pulling over cells from the origin's to the end's: keep a corner only when the last kept
        /// point can't see the point after it.
        /// </summary>
        public static void Smooth(in NavAccess access, float3 origin, float3 end, NativeList<int2> cells,
            NativeList<float3> points)
        {
            var anchor = origin;
            for (var i = 1; i < cells.Length - 1; i++)
            {
                var point = access.Grid.CellCenter(cells[i]);
                var next = i + 1 == cells.Length - 1 ? end : access.Grid.CellCenter(cells[i + 1]);
                if (!access.HasLineOfSight(anchor, next))
                {
                    points.Add(point);
                    anchor = point;
                }
            }

            points.Add(end);
        }
    }
}
