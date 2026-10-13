using System.Collections.Generic;
using HyperRTS.Editor.Maps.Checks;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Farmland
{
    /// <summary>
    /// Ties field-end tracks into the road net: ends within reach snap onto the nearest road, and runs that still lead
    /// nowhere are dropped. The symmetry seam doesn't count, since its mirror copy leads nowhere either.
    /// </summary>
    public sealed class MapFieldTrackLinks
    {
        /// <summary>A run end this close to a road's edge (metres) is carried onto the road.</summary>
        public const float Reach = 8f;

        private readonly MapPlanContext _context;
        private readonly MapAccessRoutes _routes;

        /// <summary>Links to the roads laid so far.</summary>
        public MapFieldTrackLinks(MapPlanContext context)
        {
            _context = context;
            _routes = new MapAccessRoutes(context.Shapes, context.Layout.Symmetry);
        }

        /// <summary>Snaps the run's ends onto roads within reach; false when the run should be dropped.</summary>
        public bool Link(List<Vector2> run)
        {
            Snap(run, 0);
            Snap(run, run.Count);
            return Leads(run[0]) || Leads(run[^1]);
        }

        /// <summary>Carries the end at <paramref name="insertAt"/> (0 for the start, the count for the end) onto a road.</summary>
        private void Snap(List<Vector2> run, int insertAt)
        {
            var end = insertAt == 0 ? run[0] : run[^1];
            if (MapRoadEnds.MeetsRoad(_context.Shapes, null, end))
            {
                return;
            }

            var nearest = _routes.Nearest(end);
            if (nearest.Count == 0)
            {
                return;
            }

            var (road, point, distance) = nearest[0];
            var gap = distance - road.Width * 0.5f;
            if (gap > Reach || !_routes.Clear(end, point))
            {
                return;
            }

            run.Insert(insertAt, point);
        }

        private bool Leads(Vector2 end) =>
            MapRoadEnds.LeadsSomewhere(_context.Layout, _context.Shapes, null, end, _context.Palette.baseRadius);
    }
}
