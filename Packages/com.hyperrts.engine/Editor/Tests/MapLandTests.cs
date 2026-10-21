using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Planning.Farmland;
using HyperRTS.Editor.Maps.Refine;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Map land planning: sector mapping, land distances, symmetric farmland and the spec refiners.</summary>
    public class MapLandTests
    {
        [TestCase(MapSymmetry.Rotate180)]
        [TestCase(MapSymmetry.MirrorXName)]
        [TestCase("rotate-4")]
        public void ToSector_FindsTheAuthoredPointEveryCopyCameFrom(string name)
        {
            var symmetry = MapSymmetry.Parse(name);
            var authored = name == MapSymmetry.MirrorXName ? new Vector2(-30f, 55f) : new Vector2(42f, -17f);
            if (name == "rotate-4")
            {
                authored = new Vector2(50f, 20f);
            }

            foreach (var copy in symmetry.Copies)
            {
                var back = symmetry.ToSector(copy.Apply(authored), out _);
                Assert.AreEqual(authored.x, back.x, 1e-3f);
                Assert.AreEqual(authored.y, back.y, 1e-3f);
            }
        }

        [Test]
        public void LandDistance_IsTheDistanceToTheNearestSeed()
        {
            var distance = MapLandDistance.To(20, 20, (x, z) => x == 5 && z == 5);

            Assert.AreEqual(0f, distance[5 * 20 + 5]);
            Assert.AreEqual(4f, distance[5 * 20 + 9], 0.01f);
            Assert.AreEqual(Mathf.Sqrt(32f), distance[9 * 20 + 9], 0.35f);
        }

        [Test]
        public void Farmland_GrowsTheSameCropInEveryCopyOfAField()
        {
            var context = Context(MapSymmetry.Rotate180);
            new MapFarmlandPlanner(context).Plan();

            var land = context.Land;
            var rotation = context.Layout.Symmetry.Copies[1];
            var checkedFields = 0;
            for (var x = -90f; x <= 90f; x += 15f)
            {
                for (var z = -90f; z <= -10f; z += 15f)
                {
                    var here = land.At(new Vector2(x, z));
                    var there = land.At(rotation.Apply(new Vector2(x, z)));
                    if (here?.Kind != MapLandKind.Field)
                    {
                        continue;
                    }

                    Assert.AreEqual(MapLandKind.Field, there?.Kind, $"the copy of ({x}, {z}) is a field too");
                    Assert.AreEqual(here.Surface, there.Surface, $"same crop at ({x}, {z}) and its copy");
                    checkedFields++;
                }
            }

            Assert.Greater(checkedFields, 10);
        }

        [Test]
        public void Farmland_LeavesAWeedyMarginRoundEachField()
        {
            var context = Context(MapSymmetry.Rotate180);
            new MapFarmlandPlanner(context).Plan();

            var margins = 0;
            var fields = 0;
            context.Land.ForEach((x, z, _) =>
            {
                var kind = context.Land.Region(x, z)?.Kind;
                margins += kind == MapLandKind.Margin ? 1 : 0;
                fields += kind == MapLandKind.Field ? 1 : 0;
            });

            Assert.Greater(fields, margins, "fields are mostly crop");
            Assert.Greater(margins, fields / 40, "but every field has a margin");
        }

        [Test]
        public void RiverRefiner_KeepsTheEndsAndCrossingsInPlace()
        {
            var river = new MapRiverSpec
            {
                points = new List<MapPoint> { new(-100f, -30f), new(-60f, -10f), new(-20f, -25f), new(0f, 0f) },
                meander = 8f,
                crossings = new List<MapCrossingSpec> { new() { along =0.5f, kind = MapCrossingSpec.Ford } },
            };
            var authored = MapGeometry.ToVectors(river.points);
            var crossing = MapGeometry.PointAlong(authored, 0.5f * MapGeometry.Length(authored), out _);

            MapRiverRefiner.Refine(river, 7, 0);

            var refined = MapGeometry.ToVectors(river.points);
            Assert.Greater(refined.Count, authored.Count, "the curve is sampled densely");
            Assert.AreEqual(authored[0], refined[0]);
            Assert.AreEqual(authored[^1], refined[^1], "the end at the centre still meets the copy");
            var moved = MapGeometry.PointAlong(refined, river.crossings[0].along * MapGeometry.Length(refined), out _);
            Assert.Less((moved - crossing).magnitude, 2.5f, "the ford stays where it was authored");
        }

        [Test]
        public void RavineRefiner_GrowsBranchesInsideTheSector()
        {
            var spec = new MapSpec
            {
                symmetry = MapSymmetry.Rotate180,
                seed = 3,
                ravines = new List<MapRavineSpec>
                {
                    new() { points = new List<MapPoint> { new(-80f, -40f), new(-60f, -90f), new(-40f, -150f) }, branches = 3 },
                },
            };

            MapRavineRefiner.Refine(spec, MapSymmetry.Parse(spec.symmetry));

            Assert.Greater(spec.ravines.Count, 1, "at least one branch fits");
            foreach (var branch in spec.ravines.Skip(1))
            {
                Assert.IsTrue(branch.points.All(p => p.z <= 0.5f), "branches stay in the authored half");
                Assert.Less(branch.width, spec.ravines[0].width);
            }
        }

        private static MapPlanContext Context(string symmetry)
        {
            var spec = new MapSpec
            {
                name = "Test", climate = "Test", mode = "1v1", seed = 11,
                size = new MapPoint(240f, 240f), symmetry = symmetry,
                terrain = new MapTerrainSpec { resolution = 257, backdropWidth = 20f, relief = 0f },
                spawns = new List<MapSpawnSpec> { new() { pos = new MapPoint(0f, -100f) } },
                farmland = new List<MapFarmlandSpec> { new() { kind = "Farms" } },
            };
            var issues = new MapIssues();
            var layout = MapExpander.Expand(spec, issues);
            var palette = new MapPalette();
            palette.baseRadius = 15f;
            var climate = Climate();
            var shapes = new MapShapes(layout, palette, climate);
            var heights = MapHeightShaper.Shape(layout, shapes);
            var placements = MapPropPlacer.Place(layout, shapes, palette, climate, issues);
            Assert.IsFalse(issues.HasErrors, issues.ToString());
            return new MapPlanContext(layout, shapes, heights, palette, climate, placements, issues);
        }

        private static MapClimate Climate()
        {
            var climate = new MapClimate();
            climate.name = "Test";
            MapClimateFixture.AddSurfaces(climate, "Wheat", "Sunflower", "Plowed", "Margin", "Meadow");

            climate.groundSurface = "Meadow";
            climate.farmlandKits.Add(new MapFarmlandKit
            {
                key = "Farms", fieldSize = new Vector2(60f, 40f), marginSurface = "Margin", minField = 200f,
                crops = new List<MapCropShare> { new() { surface = "Wheat" }, new() { surface = "Sunflower" }, new() { surface = "Plowed" } },
            });
            return climate;
        }
    }
}
