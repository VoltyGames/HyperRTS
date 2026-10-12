using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Runs an access track from each prop's pad to the nearest road: for every authored-sector placement whose
    /// palette entry names an access road class, a straight track from just inside the pad edge to the nearest point
    /// of the nearest road it can reach without crossing water or a ravine. Tracks are laid in every copy of the
    /// sector; a prop with no clear route is a warning.
    /// </summary>
    public sealed class MapAccessPlanner : MapLandPass
    {
        /// <summary>The track starts this far inside the pad's edge, so it grows out of the pad.</summary>
        private const float PadOverlap = 2f;

        /// <summary>A pad this close to a road's edge already meets it.</summary>
        private const float Beside = 1.5f;

        private const float TrackStep = 3f;

        public MapAccessPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            var routes = new MapAccessRoutes(Context.Shapes, Symmetry);
            var roads = new MapRoadPlanner(Context);
            foreach (var placement in Context.Placements.All)
            {
                var key = placement.Entry.access;
                if (string.IsNullOrEmpty(key) || !Symmetry.InSector(placement.Position))
                {
                    continue;
                }

                if (!Connect(routes, roads, placement, Climate.RoadClass(key)))
                {
                    var at = new MapPoint(placement.Position.x, placement.Position.y);
                    Context.Issues.Warn($"Prop '{placement.Entry.key}' at {at}: no access track laid, no road in the sector can be reached without crossing water or a ravine.");
                }
            }
        }

        /// <summary>Lays the track to the nearest reachable road; false when none can be reached.</summary>
        private static bool Connect(MapAccessRoutes routes, MapRoadPlanner roads, MapPlacement placement,
            MapRoadClass track)
        {
            var entry = placement.Entry;
            var padRadius = placement.Radius + (string.IsNullOrEmpty(entry.pad) ? 0f : entry.padMargin);
            var candidates = routes.Nearest(placement.Position);
            if (candidates.Count > 0 && Meets(candidates[0], padRadius))
            {
                return true;
            }

            foreach (var (_, point, _) in candidates)
            {
                var direction = (point - placement.Position).normalized;
                var start = placement.Position + direction * Mathf.Max(placement.Radius, padRadius - PadOverlap);
                if (routes.Clear(start, point))
                {
                    roads.LayTrack(track, MapCurves.Resample(new List<Vector2> { start, point }, TrackStep));
                    return true;
                }
            }

            return false;
        }

        private static bool Meets((MapRoad Road, Vector2 Point, float Distance) nearest, float padRadius) =>
            nearest.Distance - nearest.Road.Width * 0.5f <= padRadius + Beside;
    }
}
