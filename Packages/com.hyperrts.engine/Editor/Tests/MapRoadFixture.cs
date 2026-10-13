using System.Collections.Generic;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Specs, climates and contexts the road tests share.</summary>
    internal static class MapRoadFixture
    {
        /// <summary>A 240 m rotate-180 map: the authored sector is z at or under 0, the playable edge is at ±120.</summary>
        public static MapSpec Spec() => new()
        {
            name = "Test", climate = "Test", mode = "1v1", seed = 11,
            size = new MapPoint(240f, 240f), symmetry = MapSymmetry.Rotate180,
            terrain = new MapTerrainSpec { resolution = 257, margin = 20f, relief = 0f },
            spawns = new List<MapSpawnSpec> { new() { pos = new MapPoint(0f, -100f) } },
        };

        public static MapRoadSpec Road(string kind, params Vector2[] points) => new()
        {
            kind = kind,
            points = new List<MapPoint>(System.Array.ConvertAll(points, p => new MapPoint(p.x, p.y))),
        };

        /// <summary>Asphalt (8 m, rank 2, 25 m curves) and Dirt (4 m, rank 0, 10 m curves) with their surfaces.</summary>
        public static MapClimate Climate()
        {
            var climate = new MapClimate();
            climate.name = "Test";
            foreach (var key in new[] { "Asphalt", "Dirt", "Verge", "Meadow" })
            {
                climate.layers.Add(new MapClimateLayer { key = key });
                climate.surfaces.Add(new MapClimateSurface { key = key, layer = key, edgeWidth = 1f, edgeJitter = 1f });
            }

            climate.groundSurface = "Meadow";
            climate.roadClasses.Add(new MapRoadClass
            {
                key = "Asphalt", width = 8f, surface = "Asphalt", vergeSurface = "Verge", vergeWidth = 2f, rank = 2, curve = 25f,
            });
            climate.roadClasses.Add(new MapRoadClass
            {
                key = "Dirt", width = 4f, surface = "Dirt", vergeSurface = "Verge", vergeWidth = 1f, rank = 0, curve = 10f,
            });
            return climate;
        }

        public static MapLayout Layout(MapSpec spec)
        {
            var issues = new MapIssues();
            var layout = MapExpander.Expand(spec, issues);
            Assert.IsFalse(issues.HasErrors, issues.ToString());
            return layout;
        }

        public static MapShapes Shapes(MapLayout layout, MapClimate climate) =>
            new(layout, new MapPalette(), climate);

        public static MapPlanContext Context(MapSpec spec, MapClimate climate, MapIssues issues)
        {
            var layout = Layout(spec);
            var palette = new MapPalette();
            palette.baseRadius = 15f;
            var shapes = new MapShapes(layout, palette, climate);
            var heights = MapHeightShaper.Shape(layout, shapes);
            var placements = MapPropPlacer.Place(layout, shapes, palette, climate, issues);
            Assert.IsFalse(issues.HasErrors, issues.ToString());
            return new MapPlanContext(layout, shapes, heights, palette, climate, placements, issues);
        }

        public static MapRoad RoadShape(MapRoadClass roadClass, bool generated, params Vector2[] points) =>
            new(new MapLine(new List<Vector2>(points), roadClass.width, roadClass.width * 0.5f + MapShapes.RoadShoulder),
                roadClass, generated);

        /// <summary>The smallest radius round any three consecutive points of a line.</summary>
        public static float TightestRadius(IReadOnlyList<Vector2> line)
        {
            var tightest = float.PositiveInfinity;
            for (var i = 1; i + 1 < line.Count; i++)
            {
                tightest = Mathf.Min(tightest, MapFillet.Circumradius(line[i - 1], line[i], line[i + 1]));
            }

            return tightest;
        }
    }
}
