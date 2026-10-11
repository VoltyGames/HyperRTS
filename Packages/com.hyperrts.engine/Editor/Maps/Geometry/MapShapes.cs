using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>The layout's rivers, roads, ravines and crossings in map space, built once and shared by every pass.</summary>
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

        public MapShapes(MapLayout layout, MapPalette palette, MapIssues issues)
        {
            var water = layout.Terrain.waterLevel;
            var overhang = layout.Terrain.margin + 10f;
            foreach (var river in layout.Rivers)
            {
                var spec = river.Placed;
                var points = MapGeometry.ExtendAtEdge(MapGeometry.ToVectors(spec.points), layout.Bounds, overhang);
                var line = new MapLine(points, spec.width, spec.valleyWidth * 0.5f, null);
                Rivers.Add((line, spec));
                foreach (var crossing in spec.crossings)
                {
                    AddCrossing(MapGeometry.ToVectors(spec.points), crossing, water, palette, issues);
                }
            }

            foreach (var road in layout.Roads)
            {
                var spec = road.Placed;
                var points = MapGeometry.ExtendAtEdge(MapGeometry.ToVectors(spec.points), layout.Bounds, overhang);
                Roads.Add(new MapLine(points, spec.width, spec.width * 0.5f + RoadShoulder, spec.kind));
            }

            foreach (var ravine in layout.Ravines)
            {
                var spec = ravine.Placed;
                Ravines.Add((new MapLine(MapGeometry.ToVectors(spec.points), spec.width, spec.width * 0.5f, null), spec));
            }
        }

        public List<(MapLine Line, MapRiverSpec Spec)> Rivers { get; } = new();

        public List<MapLine> Roads { get; } = new();

        public List<(MapLine Line, MapRavineSpec Spec)> Ravines { get; } = new();

        public List<MapCrossing> Crossings { get; } = new();

        /// <summary>Distance to the nearest river's water edge (negative over the water), or infinity far from rivers.</summary>
        public float RiverEdgeDistance(Vector2 point)
        {
            var best = float.PositiveInfinity;
            foreach (var (line, spec) in Rivers)
            {
                best = Mathf.Min(best, line.Distance(point) - spec.width * 0.5f);
            }

            return best;
        }

        /// <summary>Distance to the nearest road's edge (negative on the road), or infinity far from roads.</summary>
        public float RoadEdgeDistance(Vector2 point)
        {
            var best = float.PositiveInfinity;
            foreach (var road in Roads)
            {
                best = Mathf.Min(best, road.Distance(point) - road.Width * 0.5f);
            }

            return best;
        }

        /// <summary>True inside a ravine's cut, where nothing is built.</summary>
        public bool InRavine(Vector2 point)
        {
            foreach (var (line, _) in Ravines)
            {
                if (line.Distance(point) < line.Width * 0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Crossing positions are fractions of the authored river, before it was run on past the edge.</summary>
        private void AddCrossing(List<Vector2> river, MapCrossingSpec spec, float water, MapPalette palette,
            MapIssues issues)
        {
            var width = Rivers[^1].Spec.width;
            var position = MapGeometry.PointAlong(river, spec.t * MapGeometry.Length(river), out var direction);

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

            if (spec.kind != MapCrossingSpec.Bridge)
            {
                issues.Error($"Crossing kind '{spec.kind}' is neither '{MapCrossingSpec.Bridge}' nor '{MapCrossingSpec.Ford}'.");
                return;
            }

            var entry = palette.Entry(spec.prop);
            if (entry == null)
            {
                issues.Error($"Bridge prop '{spec.prop}' is not in the palette.");
                return;
            }

            if (entry.size.y < width + 4f)
            {
                issues.Error($"Bridge '{spec.prop}' ({entry.size.y} m) is too short for a {width} m river.");
            }

            Crossings.Add(new MapCrossing(spec.kind, spec.prop, position, across, entry.size.x, entry.size.y,
                water + DeckClearance));
        }
    }
}
