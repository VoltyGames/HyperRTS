using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Refine
{
    /// <summary>
    /// Lays each authored road straight over the bridge or ford it uses, in line with the crossing, before its corners
    /// are rounded. A crossing spans square to its river, whatever way the road was drawn.
    /// </summary>
    public static class MapRoadCrossings
    {
        /// <summary>Metres of straight road before a crossing's level ground, room for the bend onto it.</summary>
        public const float Lead = 6f;

        /// <summary>Straightens <paramref name="roads"/> over the crossings of the refined <paramref name="rivers"/>.</summary>
        public static void Align(List<MapRoadSpec> roads, List<MapRiverSpec> rivers, MapClimate climate, MapDecks decks)
        {
            foreach (var river in rivers)
            {
                var channel = MapGeometry.ToVectors(river.points);
                foreach (var crossing in river.crossings)
                {
                    var position = MapGeometry.PointAlong(channel, crossing.along * MapGeometry.Length(channel), out var flow);
                    var across = MapGeometry.Left(flow);
                    var reach = HalfLength(river, crossing, decks) + Lead;
                    foreach (var road in roads)
                    {
                        var line = MapGeometry.ToVectors(road.points);
                        if (MapGeometry.DistanceToPolyline(line, position, out var middle) > crossing.width)
                        {
                            continue;
                        }

                        if (Crosses(line, middle, reach, position, across))
                        {
                            var clear = climate.RoadClass(road.kind).cornerRadius;
                            road.points = MapGeometry.ToPoints(Lay(line, middle, position, across, reach, clear));
                        }
                    }
                }
            }
        }

        /// <summary>Whether the road runs from one bank to the other round middle, rather than along one bank.</summary>
        private static bool Crosses(List<Vector2> road, float middle, float reach, Vector2 position, Vector2 across)
        {
            var before = Vector2.Dot(MapGeometry.PointAlong(road, Mathf.Max(0f, middle - reach), out _) - position, across);
            var after = Vector2.Dot(MapGeometry.PointAlong(road, middle + reach, out _) - position, across);
            return before * after < 0f;
        }

        /// <summary>From a crossing's middle to where its level ground ends: a bridge's approach slabs, a ford's banks.</summary>
        private static float HalfLength(MapRiverSpec river, MapCrossingSpec crossing, MapDecks decks)
        {
            if (crossing.kind == MapCrossingSpec.Ford)
            {
                return MapShapes.FordSpan(river.width) * 0.5f;
            }

            return decks.Find(crossing.prop).Reach;
        }

        /// <summary>
        /// The road with a straight run, reach metres either way of position along across, in place of its points round
        /// middle (metres along it). Ends inside the run stay put; points within clear metres of it go, as no bend fits.
        /// </summary>
        private static List<Vector2> Lay(List<Vector2> road, float middle, Vector2 position, Vector2 across, float reach,
            float clear)
        {
            MapGeometry.PointAlong(road, middle, out var direction);
            var heading = Vector2.Dot(direction, across) < 0f ? -across : across;
            var from = middle - reach;
            var to = middle + reach;

            var laid = new List<Vector2>();
            var after = new List<Vector2>();
            var walked = 0f;
            for (var i = 0; i < road.Count; i++)
            {
                walked += i > 0 ? (road[i] - road[i - 1]).magnitude : 0f;
                var end = i == 0 || i == road.Count - 1;
                if (walked < from - clear || (end && walked < from))
                {
                    laid.Add(road[i]);
                }
                else if (walked > to + clear || (end && walked > to))
                {
                    after.Add(road[i]);
                }
            }

            laid.Add(from > 0f ? position - heading * reach : road[0]);
            laid.Add(to < walked ? position + heading * reach : road[^1]);
            laid.AddRange(after);
            return laid;
        }
    }
}
