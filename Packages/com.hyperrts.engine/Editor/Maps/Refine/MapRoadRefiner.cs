using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Refine
{
    /// <summary>
    /// Rounds authored roads into curves and joins road ends to the roads they meet (<see cref="MapRoadJoiner"/>).
    /// Ends on the seam or playable edge stay put.
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
                var rounded = MapCornerRounding.Round(MapGeometry.ToVectors(road.points), roadClass.cornerRadius, Step);
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
