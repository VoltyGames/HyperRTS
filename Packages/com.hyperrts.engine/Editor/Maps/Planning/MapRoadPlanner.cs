using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// The land of every road: the carriageway, then the grass and weed verge either side. Passes that lay tracks
    /// (field ends, access to a pad) lay them through <see cref="LayTrack"/>.
    /// </summary>
    public sealed class MapRoadPlanner : MapLandPass
    {
        /// <summary>Track ends this close (metres) are the same end: on a symmetry axis a copy lands on the track itself.</summary>
        private const float SameEnd = 0.5f;

        /// <summary>How far a verge's outer edge wanders, as a share of its width, and over how many metres.</summary>
        private const float VergeWear = 0.6f;
        private const float VergeWearWavelength = 9f;

        public MapRoadPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            foreach (var road in Context.Shapes.Roads)
            {
                Claim(road);
            }
        }

        /// <summary>Lays a track chosen in the authored sector: the road in every copy (once on an axis), and its land.</summary>
        public void LayTrack(MapRoadClass track, List<Vector2> sectorLine)
        {
            var laid = new List<List<Vector2>>();
            foreach (var copy in Symmetry.Copies)
            {
                var points = sectorLine.ConvertAll(copy.Apply);
                if (laid.Exists(other => Same(other, points)))
                {
                    continue;
                }

                laid.Add(points);
                var line = new MapLine(points, track.width, track.width * 0.5f + track.vergeWidth + MapShapes.RoadShoulder);
                var road = new MapRoad(line, track, true);
                Context.Shapes.AddRoad(road);
                Claim(road);
            }
        }

        private void Claim(MapRoad road)
        {
            var roadClass = road.Class;
            var half = road.Width * 0.5f;
            var surface = Context.RoadRegion(roadClass.surface, roadClass.impactClass);
            if (roadClass.vergeWidth <= 0f)
            {
                Land.ClaimWhere(road.Line.Bounds, surface, p => road.Line.Distance(p) < half);
                return;
            }

            var verge = Context.Region(MapLandKind.Verge, roadClass.vergeSurface);
            var wear = Context.Noise(MapNoiseChannel.Verges);
            Land.ClaimEach(road.Line.Bounds, p =>
            {
                var distance = road.Line.Distance(p);
                if (distance < half)
                {
                    return surface;
                }

                var width = roadClass.vergeWidth * (1f + VergeWear * wear.Sample(p, VergeWearWavelength));
                return distance < half + width ? verge : MapLand.Unclaimed;
            });
        }

        /// <summary>Lines with the same ends either way round.</summary>
        private static bool Same(List<Vector2> a, List<Vector2> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            var forward = Near(a[0], b[0]) && Near(a[^1], b[^1]);
            var backward = Near(a[0], b[^1]) && Near(a[^1], b[0]);
            return forward || backward;
        }

        private static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < SameEnd * SameEnd;
    }
}
