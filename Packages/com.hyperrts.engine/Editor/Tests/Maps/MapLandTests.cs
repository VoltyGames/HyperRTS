using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Planning.Farmland;
using HyperRTS.Editor.Maps.Refine;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>Map land planning: land distances, prop pads, symmetric farmland and the river and ravine refiners.</summary>
    public class MapLandTests
    {
        [Test]
        public void LandDistance_IsTheDistanceToTheNearestSeed()
        {
            var distance = MapLandDistance.To(20, 20, (x, z) => x == 5 && z == 5);

            Assert.AreEqual(0f, distance[5 * 20 + 5]);
            Assert.AreEqual(4f, distance[5 * 20 + 9], 0.01f);
            Assert.AreEqual(Mathf.Sqrt(32f), distance[9 * 20 + 9], 0.35f);
        }

        // A 6 m prop with a 3 m pad margin and no edge wander spans 12 m.
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void APadCorner_IsPaddedOnlyWhenSquared(bool round, bool cornerPadded)
        {
            var prop = new Vector2(20f, -100f);
            var context = MapFixture.Context(MapFixture.Spec(), MapFixture.Climate(), new MapIssues());
            var entry = new MapProp
            {
                key = "Dump", size = new Vector2(6f, 6f), pad = "Dirt", padMargin = 3f, padWander = 0f, padRound = round,
            };
            context.Placements.All.Add(new MapPlacement(entry, prop, 0f, 1f));

            new MapBasePlanner(context).Plan();

            Assert.AreEqual(MapLandKind.Pad, context.Land.KindAt(prop));
            Assert.AreEqual(MapLandKind.Pad, context.Land.KindAt(prop + new Vector2(5.5f, 0f)), "edge midpoint");
            Assert.AreEqual(cornerPadded, context.Land.KindAt(prop + new Vector2(5.5f, 5.5f)) == MapLandKind.Pad);
        }

        [Test]
        public void Farmland_GrowsTheSameCropInEveryCopyOfAField()
        {
            var context = FarmContext();
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
            var context = FarmContext();
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
        public void Farmland_TurnsTheRowsInAHeadlandAtEachFieldEnd()
        {
            var context = FarmContext(headlandWidth: 6f);
            new MapFarmlandPlanner(context).Plan();

            // Rows run north; fields end every 60 m along z and are 40 m wide.
            foreach (var x in new[] { -60f, -20f, 20f })
            {
                var middle = context.Land.At(new Vector2(x, -30f));
                Assert.AreEqual(0f, middle.RowAngle, $"rows run along the field at ({x}, -30)");
                foreach (var z in new[] { -54f, -6f })
                {
                    var end = context.Land.At(new Vector2(x, z));
                    Assert.AreEqual(MapLandKind.Field, end.Kind, $"({x}, {z}) is sown");
                    Assert.AreEqual(middle.Surface, end.Surface, $"the headland at ({x}, {z}) grows the field's crop");
                    Assert.AreEqual(90f, end.RowAngle, $"rows at ({x}, {z}) run along the field end");
                }
            }
        }

        [Test]
        public void RiverRefiner_KeepsTheEndsAndCrossingsInPlace()
        {
            var river = new MapRiverSpec
            {
                points = new List<MapPoint> { new(-100f, -30f), new(-60f, -10f), new(-20f, -25f), new(0f, 0f) },
                meander = 8f,
                crossings = new List<MapCrossingSpec> { new() { along = 0.5f, kind = MapCrossingSpec.Ford } },
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

        private static MapPlanContext FarmContext(float headlandWidth = 0f)
        {
            var spec = MapFixture.Spec();
            spec.farmland.Add(new MapFarmlandSpec { kind = "Farms" });
            return MapFixture.Context(spec, Climate(headlandWidth), new MapIssues());
        }

        private static MapClimate Climate(float headlandWidth)
        {
            var climate = new MapClimate();
            climate.name = "Test";
            MapFixture.AddSurfaces(climate, "Wheat", "Sunflower", "Plowed", "Margin", "Meadow");

            climate.groundSurface = "Meadow";
            climate.farmlandKits.Add(new MapFarmlandKit
            {
                key = "Farms", fieldSize = new Vector2(60f, 40f), marginSurface = "Margin", minField = 200f,
                headlandWidth = headlandWidth,
                crops = new List<MapCropShare> { new() { surface = "Wheat" }, new() { surface = "Sunflower" }, new() { surface = "Plowed" } },
            });
            return climate;
        }
    }
}
