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
        private const int WidthNoiseChannel = 20;

        public MapShapes(MapLayout layout, MapPalette palette, MapClimate climate)
        {
            var water = layout.Terrain.waterLevel;
            var overhang = layout.Terrain.margin + 10f;
            var width = new MapNoise(layout.Symmetry, layout.Spec.seed, WidthNoiseChannel);
            foreach (var river in layout.Rivers)
            {
                var spec = river.Placed;
                var points = MapGeometry.ExtendAtEdge(MapGeometry.ToVectors(spec.points), layout.Bounds, overhang);
                Rivers.Add(new MapRiverShape(new MapLine(points, spec.width, spec.valleyWidth * 0.5f), spec, width));
                foreach (var crossing in spec.crossings)
                {
                    AddCrossing(MapGeometry.ToVectors(spec.points), crossing, water, palette);
                }
            }

            foreach (var road in layout.Roads)
            {
                AddAuthoredRoad(road.Placed, layout, climate, overhang);
            }

            foreach (var ravine in layout.Ravines)
            {
                var spec = ravine.Placed;
                Ravines.Add((new MapLine(MapGeometry.ToVectors(spec.points), spec.width, spec.width * 0.5f + spec.rim), spec));
            }
        }

        public List<MapRiverShape> Rivers { get; } = new();

        public List<MapRoad> Roads { get; } = new();

        public List<(MapLine Line, MapRavineSpec Spec)> Ravines { get; } = new();

        public List<MapCrossing> Crossings { get; } = new();

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

        /// <summary>True inside a ravine's cut, grown by <paramref name="margin"/> metres, where nothing is built.</summary>
        public bool InRavine(Vector2 point, float margin = 0f)
        {
            foreach (var (line, _) in Ravines)
            {
                if (line.Distance(point) < line.Width * 0.5f + margin)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddAuthoredRoad(MapRoadSpec spec, MapLayout layout, MapClimate climate, float overhang)
        {
            var roadClass = climate.RoadClass(spec.kind);
            var width = roadClass.width;
            var points = MapGeometry.ExtendAtEdge(MapGeometry.ToVectors(spec.points), layout.Bounds, overhang);
            Roads.Add(new MapRoad(new MapLine(points, width, width * 0.5f + RoadShoulder), roadClass, false));
        }

        /// <summary>Crossing positions are fractions of the authored river, before it was run on past the edge.</summary>
        private void AddCrossing(List<Vector2> river, MapCrossingSpec spec, float water, MapPalette palette)
        {
            var width = Rivers[^1].Spec.width;
            var position = MapGeometry.PointAlong(river, spec.t * MapGeometry.Length(river), out var direction);
            Rivers[^1].Pin(position);

            // A crossing where a river meets its own copy (the centre) is listed by both copies; keep one.
            if (Crossings.Exists(c => (c.Position - position).sqrMagnitude < 1f))
            {
                return;
            }

            var across = new Vector2(-direction.y, direction.x);
            if (spec.kind == MapCrossingSpec.Ford)
            {
                Crossings.Add(new MapCrossing(spec.kind, null, position, across, spec.width, width + FordBanks,
                    water - FordWade));
                return;
            }

            var entry = palette.Entry(spec.prop);
            Crossings.Add(new MapCrossing(spec.kind, spec.prop, position, across, entry.size.x, entry.size.y,
                water + DeckClearance));
        }
    }
}
