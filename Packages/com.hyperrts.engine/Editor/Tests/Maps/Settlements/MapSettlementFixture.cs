using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Planning.Settlements;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Tests.Maps.Settlements
{
    /// <summary>
    /// A 240 m rotate-180 map with one village on a straight east-west road at z = -60 (in the authored half),
    /// built in code: a climate with the village surfaces, a palette with one prefab per kit key, and the land planners
    /// that run before settlements. Dispose destroys the assets it made.
    /// </summary>
    internal sealed class MapSettlementFixture : IDisposable
    {
        public const float RoadZ = -60f;
        public const float BaseRadius = 15f;
        private readonly List<Object> _made = new();

        private MapSettlementFixture()
        {
        }

        public MapPlanContext Context { get; private set; }

        public MapPalette Palette { get; private set; }

        public IReadOnlyList<MapPlacement> Placements => Context.Placements.All;

        /// <summary>The placements of one palette key in the authored half (z at or below 0).</summary>
        public List<MapPlacement> InSector(string key) =>
            Placements.Where(p => p.Entry.key == key && p.Position.y <= 0f).ToList();

        /// <summary>
        /// Builds the map and runs the planners up to and including settlements. <paramref name="spawn"/> is the
        /// authored base, <paramref name="edit"/> adjusts the village kit before planning.
        /// </summary>
        public static MapSettlementFixture Plan(bool river = false, bool road = true, Vector2? spawn = null,
            int count = 6, Action<MapSettlementKit> edit = null)
        {
            var fixture = new MapSettlementFixture();
            fixture.Build(river, road, spawn ?? new Vector2(0f, -100f), count, edit);
            var context = fixture.Context;
            new MapWaterPlanner(context).Plan();
            new MapRoadPlanner(context).Plan();
            new MapBankPlanner(context).Plan();
            new MapBasePlanner(context).Plan();
            new MapSettlementPlanner(context).Plan();
            return fixture;
        }

        /// <summary>The asset check's issues for the fixture's climate and palette, the village kit edited first.</summary>
        public static MapIssues CheckAssets(Action<MapSettlementKit> edit)
        {
            using var fixture = new MapSettlementFixture();
            var issues = new MapIssues();
            MapAssetCheck.Check(fixture.MakeClimate(), fixture.MakePalette(edit), issues);
            return issues;
        }

        public void Dispose()
        {
            foreach (var made in _made)
            {
                Object.DestroyImmediate(made);
            }
        }

        private void Build(bool river, bool road, Vector2 spawn, int count, Action<MapSettlementKit> edit)
        {
            var spec = MapFixture.Spec();
            spec.spawns[0].pos = new MapPoint(spawn.x, spawn.y);
            spec.settlements.Add(new MapSettlementSpec { center = new MapPoint(0f, RoadZ), radius = 80f, kind = "Village", count = count });
            if (road)
            {
                spec.roads.Add(new MapRoadSpec
                {
                    kind = "Street", points = new List<MapPoint> { new(-110f, RoadZ), new(110f, RoadZ) },
                });
            }

            if (river)
            {
                spec.rivers.Add(new MapRiverSpec
                {
                    points = new List<MapPoint> { new(-110f, -85f), new(110f, -85f) }, width = 10f, valleyWidth = 30f,
                });
            }

            var issues = new MapIssues();
            var layout = MapExpander.Expand(spec, issues);
            Palette = MakePalette(edit);
            var climate = MakeClimate();
            var shapes = new MapShapes(layout, Palette, climate);
            var heights = MapHeightShaper.Shape(layout, shapes);
            var placements = MapPropPlacer.Place(layout, shapes, Palette, climate, issues);
            Context = new MapPlanContext(layout, shapes, heights, Palette, climate, placements, issues);
        }

        private MapPalette MakePalette(Action<MapSettlementKit> edit)
        {
            var palette = MapFixture.Item<MapPalette>("Palette");
            palette.baseRadius = BaseRadius;
            palette.entries.Add(Entry("House", 10f, 8f));
            palette.entries.Add(Entry("Barn", 6f, 5f));
            palette.entries.Add(Entry("Fence", 1f, 2.5f));
            palette.entries.Add(Entry("Gate", 1.2f, 3f));
            palette.entries.Add(Entry("Pole", 1.5f, 1.5f));
            palette.entries.Add(Entry("Pipe", 0.5f, 3f));
            palette.entries.Add(Entry("PipeArch", 1f, 2.5f));
            palette.entries.Add(Entry("Well", 1.5f, 1.5f));
            var kit = MapFixture.Item<MapSettlementKit>("Village", i =>
            {
                i.plot = new Vector2(24f, 36f);
                i.plotWidthVariation = 0f;
                i.frontGap = 2f;
                i.frontYard = 5f;
                i.maxOutbuildings = 1;
                i.frontFence = "Fence";
                i.sideFence = "Fence";
                i.gate = "Gate";
                i.yardSurface = "Yard";
                i.gardenSurface = "Garden";
                i.backSurface = "Orchard";
                i.gardenShare = 0.5f;
                i.orchardTree = "Apple";
                i.orchardSpacing = 3f;
                i.streetPipe = "Pipe";
                i.streetPipeArch = "PipeArch";
                i.streetPipeMinSections = 3;
                i.streetPipeMaxGap = 6f;
                i.streetPole = "Pole";
                i.poleSpacing = 30f;
            });
            kit.houses.Add("House");
            kit.outbuildings.Add("Barn");
            kit.extras.Add(new MapPlotProp { kind = "Well", chance = 1f, zone = MapPlotZone.Yard });
            edit?.Invoke(kit);
            palette.settlementKits.Add(kit);
            return palette;
        }

        private MapClimate MakeClimate()
        {
            var climate = MapFixture.Item<MapClimate>("Test");
            MapFixture.AddSurfaces(climate, "Meadow", "Road", "Verge", "Yard", "Garden", "Orchard", "Path");

            climate.groundSurface = "Meadow";
            climate.baseSurface = "Meadow";
            climate.shoreSurface = "Meadow";
            climate.floodplainSurface = "Meadow";
            climate.trees.Add(MapFixture.Item<MapClimateTree>("Apple", i => i.prefabs.Add(Make(new GameObject("Apple")))));
            climate.roadClasses.Add(MapFixture.Item<MapRoadClass>("Street", i =>
            {
                i.width = 6f;
                i.surface = "Road";
                i.impactClass = "Soil";
                i.vergeWidth = 3f;
                i.vergeSurface = "Verge";
            }));
            return climate;
        }

        private MapProp Entry(string key, float width, float depth) =>
            MapFixture.Item<MapProp>(key, i =>
            {
                i.prefab = Make(new GameObject(key));
                i.size = new Vector2(width, depth);
            });

        private T Make<T>(T made) where T : Object
        {
            _made.Add(made);
            return made;
        }
    }
}
