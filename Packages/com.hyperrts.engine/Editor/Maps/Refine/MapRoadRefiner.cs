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

        public static void Refine(MapSpec spec, MapClimate climate, MapSymmetry symmetry)
        {
            var roads = new List<MapRefinedRoad>();
            foreach (var road in spec.roads)
            {
                var roadClass = climate.RoadClass(road.kind);
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
