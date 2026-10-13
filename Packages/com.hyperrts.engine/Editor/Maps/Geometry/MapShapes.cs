using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>
    /// The layout's rivers, roads, ravines and crossings in map space, built once and shared by every pass. Planners
    /// add the tracks they lay with <see cref="AddRoad"/>.
    /// </summary>
    public sealed class MapShapes
    {
        /// <summary>Ground beside a road eased into it (metres past its edge), so cuts and fills stay gentle.</summary>
        public const float RoadShoulder = 8f;

        /// <summary>Bridge decks stand this high over the water.</summary>
        public const float DeckClearance = 2.5f;

        /// <summary>Fords: how far the bed sits under the water, and how high units wade.</summary>
        public const float FordBed = 0.3f;
        public const float FordWade = 0.15f;

        private const float FordBanks = 8f;

        public MapShapes(MapLayout layout, MapPalette palette, MapClimate climate)
        {
            Wobble = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.Wobble);
            var water = layout.Terrain.waterLevel;
            var overhang = layout.Terrain.margin + 10f;
            var width = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.RiverWidth);
            foreach (var copy in layout.Rivers)
            {
                var spec = copy.Placed;
                var authored = MapGeometry.ToVectors(spec.points);
                var points = MapGeometry.ExtendAtEdge(authored, layout.Bounds, overhang);
                var river = new MapRiver(new MapLine(points, spec.width, spec.valleyWidth * 0.5f), spec, width);
                Rivers.Add(river);
                foreach (var crossing in spec.crossings)
                {
                    AddCrossing(river, authored, crossing, water, palette);
                }
            }

            foreach (var road in layout.Roads)
            {
                AddAuthoredRoad(road.Placed, road.Index, layout, climate, overhang);
            }

            foreach (var ravine in layout.Ravines)
            {
                var spec = ravine.Placed;
                var line = new MapLine(MapGeometry.ToVectors(spec.points), spec.width, spec.width * 0.5f + spec.rim);
                Ravines.Add(new MapRavine(line, spec, Wobble));
            }
        }

        public List<MapRiver> Rivers { get; } = new();

        public List<MapRoad> Roads { get; } = new();

        public List<MapRavine> Ravines { get; } = new();

        public List<MapCrossing> Crossings { get; } = new();

        /// <summary>The noise that wobbles ravine walls and river banks.</summary>
        public MapNoise Wobble { get; }

        /// <summary>A road laid by a planner (field or access track).</summary>
        public void AddRoad(MapRoad road) => Roads.Add(road);

        /// <summary>Distance to the nearest river's water edge (negative over the water), or infinity far from rivers.</summary>
        public float RiverEdgeDistance(Vector2 point)
        {
            var best = float.PositiveInfinity;
            foreach (var river in Rivers)
            {
                best = Mathf.Min(best, river.EdgeDistance(point));
            }

            return best;
        }

        /// <summary>Distance to the nearest road's edge (negative on the road), or infinity far from roads.</summary>
        public float RoadEdgeDistance(Vector2 point)
        {
            var best = float.PositiveInfinity;
            foreach (var road in Roads)
            {
                best = Mathf.Min(best, road.Line.Distance(point) - road.Width * 0.5f);
            }

            return best;
        }

        /// <summary>A spot set back from the nearest road's edge on the point's side, and the road's yaw there.</summary>
        public bool RoadsideSpot(Vector2 point, float setback, out Vector2 spot, out float yaw)
        {
            spot = point;
            yaw = 0f;
            if (!NearestRoad(point, out var nearest, out var center, out var direction))
            {
                return false;
            }

            var right = MapGeometry.Right(direction);
            var side = Vector2.Dot(point - center, right) >= 0f ? 1f : -1f;
            spot = center + right * side * (nearest.Width * 0.5f + setback);
            yaw = MapGeometry.Yaw(direction);
            return true;
        }

        /// <summary>The road whose edge is nearest the point, the closest point on its centre line and its heading there.</summary>
        public bool NearestRoad(Vector2 point, out MapRoad nearest, out Vector2 center, out Vector2 direction)
        {
            nearest = null;
            var along = 0f;
            var best = float.PositiveInfinity;
            foreach (var road in Roads)
            {
                var distance = road.Line.Distance(point, out var at) - road.Width * 0.5f;
                if (distance < best)
                {
                    nearest = road;
                    along = at;
                    best = distance;
                }
            }

            center = point;
            direction = Vector2.up;
            if (nearest == null)
            {
                return false;
            }

            center = nearest.Line.Path.PointAt(along, out direction);
            return true;
        }

        /// <summary>True inside a ravine's cut, grown by <paramref name="margin"/> metres, where nothing is built.</summary>
        public bool InRavine(Vector2 point, float margin = 0f)
        {
            foreach (var ravine in Ravines)
            {
                var line = ravine.Line;
                if (line.Distance(point) < line.Width * 0.5f + margin)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddAuthoredRoad(MapRoadSpec spec, int index, MapLayout layout, MapClimate climate, float overhang)
        {
            var roadClass = climate.RoadClass(spec.kind);
            var width = roadClass.width;
            var points = MapGeometry.ExtendAtEdge(MapGeometry.ToVectors(spec.points), layout.Bounds, overhang);
            Roads.Add(new MapRoad(new MapLine(points, width, width * 0.5f + RoadShoulder), roadClass, false, index));
        }

        /// <summary>Crossing positions are fractions of the authored river, before it was run on past the edge.</summary>
        private void AddCrossing(MapRiver river, List<Vector2> authored, MapCrossingSpec spec, float water,
            MapPalette palette)
        {
            var position = MapGeometry.PointAlong(authored, spec.t * MapGeometry.Length(authored), out var direction);
            river.Pin(position);

            // A crossing where a river meets its own copy (the centre) is listed by both copies; keep one.
            if (Crossings.Exists(c => (c.Position - position).sqrMagnitude < 1f))
            {
                return;
            }

            var across = MapGeometry.Left(direction);
            if (spec.kind == MapCrossingSpec.Ford)
            {
                Crossings.Add(new MapCrossing(spec.kind, null, position, across, spec.width, river.Spec.width + FordBanks,
                    water - FordWade));
                return;
            }

            var entry = palette.Entry(spec.prop);
            Crossings.Add(new MapCrossing(spec.kind, spec.prop, position, across, entry.size.x, entry.size.y,
                water + DeckClearance));
        }
    }
}
