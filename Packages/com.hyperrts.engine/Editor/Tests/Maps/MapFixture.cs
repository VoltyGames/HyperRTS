using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>Specs, climates and plan contexts the map tests share.</summary>
    internal static class MapFixture
    {
        /// <summary>A 240 m rotate-180 map: the authored sector is z at or under 0, the playable edge is at 120.</summary>
        public static MapSpec Spec() => new()
        {
            name = "Test", climate = "Test", weather = "Clear", mode = "1v1", seed = 11,
            size = new MapPoint(240f, 240f), symmetry = MapSymmetry.Rotate180,
            terrain = new MapTerrainSpec { resolution = 257, backdropWidth = 20f, relief = 0f },
            spawns = new List<MapSpawnSpec> { new() { pos = new MapPoint(0f, -100f) } },
        };

        public static MapRoadSpec Road(string kind, params Vector2[] points) => new()
        {
            kind = kind,
            points = new List<MapPoint>(System.Array.ConvertAll(points, p => new MapPoint(p.x, p.y))),
        };

        /// <summary>A library item made in memory, keyed and named <paramref name="key"/> as the library would.</summary>
        public static T Item<T>(string key) where T : MapLibraryItem
        {
            var item = ScriptableObject.CreateInstance<T>();
            item.key = key;
            item.name = key;
            return item;
        }

        /// <summary>Asphalt (8 m, rank 2, 25 m curves) and Dirt (4 m, rank 0, 10 m curves) with their surfaces.</summary>
        public static MapClimate Climate()
        {
            var climate = Item<MapClimate>("Test");
            AddSurfaces(climate, "Asphalt", "Dirt", "Verge", "Meadow");

            climate.groundSurface = "Meadow";
            climate.weathers.Add(new MapClimateWeather { key = "Clear" });
            climate.roadClasses.Add(RoadClass("Asphalt", 8f, 2, 25f, "Paved", 2f));
            climate.roadClasses.Add(RoadClass("Dirt", 4f, 0, 10f, "Soil", 1f));
            return climate;
        }

        /// <summary>An <see cref="Item{T}(string)"/> with <paramref name="init"/> applied.</summary>
        public static T Item<T>(string key, Action<T> init) where T : MapLibraryItem
        {
            var item = Item<T>(key);
            init(item);
            return item;
        }

        private static MapRoadClass RoadClass(string key, float width, int rank, float cornerRadius, string impactClass, float vergeWidth)
        {
            var roadClass = Item<MapRoadClass>(key);
            roadClass.width = width;
            roadClass.surface = key;
            roadClass.impactClass = impactClass;
            roadClass.vergeSurface = "Verge";
            roadClass.vergeWidth = vergeWidth;
            roadClass.rank = rank;
            roadClass.cornerRadius = cornerRadius;
            return roadClass;
        }

        /// <summary>A climate surface with every required field set.</summary>
        public static MapClimateSurface Surface(string key, string layer, float edgeWidth = 1f, float edgeWander = 1f)
        {
            var surface = Item<MapClimateSurface>(key);
            surface.layer = layer;
            surface.impactClass = "Grass";
            surface.edgeWidth = edgeWidth;
            surface.edgeWander = edgeWander;
            return surface;
        }

        /// <summary>One terrain layer and one surface painting it per key.</summary>
        public static void AddSurfaces(MapClimate climate, params string[] keys) => AddSurfaces(climate, 1f, 1f, keys);

        public static void AddSurfaces(MapClimate climate, float edgeWidth, float edgeWander, params string[] keys)
        {
            foreach (var key in keys)
            {
                climate.layers.Add(Item<MapClimateLayer>(key));
                climate.surfaces.Add(Surface(key, key, edgeWidth, edgeWander));
            }
        }

        public static MapLayout Layout(MapSpec spec)
        {
            var issues = new MapIssues();
            var layout = MapExpander.Expand(spec, issues);
            Assert.IsFalse(issues.HasErrors, issues.ToString());
            return layout;
        }

        public static MapShapes Shapes(MapLayout layout, MapClimate climate) =>
            new(layout, Item<MapPalette>("Palette"), climate);

        public static MapPlanContext Context(MapSpec spec, MapClimate climate, MapIssues issues)
        {
            var layout = Layout(spec);
            var palette = Item<MapPalette>("Palette");
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
