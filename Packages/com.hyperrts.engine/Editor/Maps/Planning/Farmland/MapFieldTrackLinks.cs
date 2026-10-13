using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Farmland
{
    /// <summary>
    /// Ties field-end tracks into the road net: ends within the kit's <see cref="MapFarmlandKit.trackJoinDistance"/> join
    /// onto the nearest road, and runs that still lead nowhere are dropped. The symmetry seam doesn't count, since its
    /// mirror copy leads nowhere either.
    /// </summary>
    public sealed class MapFieldTrackLinks
    {
        private readonly MapPlanContext _context;
        private readonly float _joinDistance;
        private readonly MapAccessRoutes _routes;

        /// <summary>Links to the roads laid so far; a run end within <paramref name="joinDistance"/> of a road's edge is carried onto it.</summary>
        public MapFieldTrackLinks(MapPlanContext context, float joinDistance)
        {
            _context = context;
            _joinDistance = joinDistance;
            _routes = new MapAccessRoutes(context.Shapes, context.Layout.Symmetry);
        }

        /// <summary>Snaps the run's ends onto roads within the join distance; false when the run should be dropped.</summary>
        public bool Link(List<Vector2> run)
        {
            var startOnRoad = Snap(run, 0);
            var endOnRoad = Snap(run, run.Count);
            return Leads(run[0], startOnRoad) || Leads(run[^1], endOnRoad);
        }

        /// <summary>
        /// Carries the end at <paramref name="insertAt"/> (0 for the start, the count for the end) onto a road; true
        /// when the end is on a road, already or now.
        /// </summary>
        private bool Snap(List<Vector2> run, int insertAt)
        {
            var end = insertAt == 0 ? run[0] : run[^1];
            if (_context.Shapes.RoadEdgeDistance(end) <= MapRoadEnds.MeetDistance)
            {
                return true;
            }

            if (!_routes.TryNearestRoad(end, out var nearest))
            {
                return false;
            }

            var gap = nearest.Distance - nearest.Road.Width * 0.5f;
            if (gap > _joinDistance || !_routes.Clear(end, nearest.Point))
            {
                return false;
            }

            run.Insert(insertAt, nearest.Point);
            return true;
        }

        /// <summary>Snap already tested the road, so only the edge, settlements and bases are left.</summary>
        private bool Leads(Vector2 end, bool onRoad) =>
            onRoad || MapRoadEnds.LeadsOffRoad(_context.Layout, end, _context.Palette.baseRadius);
    }
}
