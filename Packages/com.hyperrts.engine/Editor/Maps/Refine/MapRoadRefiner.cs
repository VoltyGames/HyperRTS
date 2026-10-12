using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Refine
{
    /// <summary>
    /// Rounds authored roads into curves (corners filleted to the road class's <c>curve</c> radius) and joins road ends
    /// to the roads they meet (<see cref="MapRoadJoiner"/>). Works in the authored sector, before the symmetry copies
    /// it; ends on the seam or at the playable edge stay put, their copy or the margin continues them.
    /// </summary>
    public static class MapRoadRefiner
    {
        /// <summary>Metres between the points of a refined road.</summary>
        public const float Step = 3f;

        public static void Refine(MapSpec spec, MapClimate climate, MapSymmetry symmetry, MapIssues issues)
        {
            var roads = new List<MapRefinedRoad>();
            for (var i = 0; i < spec.roads.Count; i++)
            {
                var road = spec.roads[i];
                var roadClass = climate.RoadClass(road.kind);
                if (roadClass == null)
                {
                    issues.Error($"Road {i + 1}: road class '{road.kind}' is not in climate '{climate.name}'.");
                    continue;
                }

                if (road.points.Count < 2)
                {
                    issues.Error($"Road {i + 1} needs at least two points.");
                    continue;
                }

                var rounded = MapFillet.Round(MapGeometry.ToVectors(road.points), roadClass.curve, Step);
                roads.Add(new MapRefinedRoad(road, roadClass, rounded));
            }

            var playable = new Rect(-spec.size.ToVector2() * 0.5f, spec.size.ToVector2());
            MapRoadJoiner.Join(roads, symmetry, playable);
            foreach (var road in roads)
            {
                road.Spec.points = MapGeometry.ToPoints(road.Points);
            }
        }
    }
}
