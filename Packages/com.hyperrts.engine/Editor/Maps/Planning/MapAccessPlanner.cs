using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Runs a straight access track from each prop's pad to the nearest road it can reach without crossing water or a
    /// ravine; nearby props share one track (<see cref="MapAccessGroups"/>).
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
            var props = Context.Placements.All
                .Where(p => !string.IsNullOrEmpty(p.Entry.access) && Symmetry.InSector(p.Position))
                .ToList();
            foreach (var group in MapAccessGroups.Group(props))
            {
                if (!ConnectGroup(routes, roads, group))
                {
                    var first = group[0];
                    var at = new MapPoint(first.Position.x, first.Position.y);
                    Context.Issues.Warn($"Prop '{first.Entry.key}' at {at}: no access track laid, no road in the sector can be reached without crossing water or a ravine.");
                }
            }
        }

        /// <summary>One track for the group, from the prop nearest a road that can reach one.</summary>
        private bool ConnectGroup(MapAccessRoutes routes, MapRoadPlanner roads, List<MapPlacement> group)
        {
            var track = Climate.RoadClass(group[0].Entry.access);
            var nearestFirst = group
                .Select(p => (Placement: p, Candidates: routes.Nearest(p.Position)))
                .OrderBy(c => NearestDistance(c.Candidates));
            return nearestFirst.Any(c => Connect(routes, roads, c.Placement, c.Candidates, track));
        }

        private static float NearestDistance(List<(MapRoad Road, Vector2 Point, float Distance)> candidates) =>
            candidates.Count == 0 ? float.PositiveInfinity : candidates[0].Distance;

        /// <summary>Lays the track to the nearest reachable road; false when none can be reached.</summary>
        private static bool Connect(MapAccessRoutes routes, MapRoadPlanner roads, MapPlacement placement,
            List<(MapRoad Road, Vector2 Point, float Distance)> candidates, MapRoadClass track)
        {
            var entry = placement.Entry;
            var padRadius = placement.Radius + (string.IsNullOrEmpty(entry.pad) ? 0f : entry.padMargin);
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
