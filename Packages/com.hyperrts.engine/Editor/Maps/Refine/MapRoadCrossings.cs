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
                    var reach = HalfLength(river, crossing, decks) + Lead;
                    foreach (var road in roads)
                    {
                        var line = MapGeometry.ToVectors(road.points);
                        if (MapGeometry.DistanceToPolyline(line, position, out var middle) <= crossing.width)
                        {
                            var run = new Run(position, MapGeometry.Left(flow), reach, climate.RoadClass(road.kind).cornerRadius);
                            road.points = MapGeometry.ToPoints(run.Lay(line, middle));
                        }
                    }
                }
            }
        }

        /// <summary>From a crossing's middle to where its level ground ends: a bridge's approach slabs, a ford's banks.</summary>
        private static float HalfLength(MapRiverSpec river, MapCrossingSpec crossing, MapDecks decks)
        {
            if (crossing.kind == MapCrossingSpec.Ford)
            {
                return (river.width + MapShapes.FordBanks) * 0.5f;
            }

            var deck = decks.Find(crossing.prop);
            return deck.Size.y * 0.5f + deck.Landing;
        }

        /// <summary>
        /// A straight run through a crossing: Reach metres either way from Position along Across, with no authored
        /// point left within Clear metres of its ends, where no bend of the road's class would fit.
        /// </summary>
        private readonly struct Run
        {
            private readonly Vector2 _position;
            private readonly Vector2 _across;
            private readonly float _reach;
            private readonly float _clear;

            public Run(Vector2 position, Vector2 across, float reach, float clear)
            {
                _position = position;
                _across = across;
                _reach = reach;
                _clear = clear;
            }

            /// <summary>
            /// The road with the run in place of its points round <paramref name="middle"/> (metres along it). A road
            /// that starts or ends inside the run keeps that end where it is.
            /// </summary>
            public List<Vector2> Lay(List<Vector2> road, float middle)
            {
                new MapLineWalker(road).PointAt(middle, out var direction);
                var heading = Vector2.Dot(direction, _across) < 0f ? -_across : _across;
                var from = middle - _reach;
                var to = middle + _reach;

                var laid = new List<Vector2>();
                var after = new List<Vector2>();
                var walked = 0f;
                for (var i = 0; i < road.Count; i++)
                {
                    walked += i > 0 ? (road[i] - road[i - 1]).magnitude : 0f;
                    var end = i == 0 || i == road.Count - 1;
                    if (walked < from - _clear || (end && walked < from))
                    {
                        laid.Add(road[i]);
                    }
                    else if (walked > to + _clear || (end && walked > to))
                    {
                        after.Add(road[i]);
                    }
                }

                laid.Add(from > 0f ? _position - heading * _reach : road[0]);
                laid.Add(to < walked ? _position + heading * _reach : road[^1]);
                laid.AddRange(after);
                return laid;
            }
        }
    }
}
