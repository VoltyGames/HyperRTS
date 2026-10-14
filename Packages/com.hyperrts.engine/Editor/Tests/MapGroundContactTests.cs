using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Scenes;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>How generated props and plants meet the ground, roads, edges and each other.</summary>
    public class MapGroundContactTests
    {
        [Test]
        public void Normal_IsUpOnAPlaneAndLeansDownhillOnASlope()
        {
            var flat = Heights(_ => 3f);
            var slope = Heights(p => 0.5f * p.x);

            Assert.That(Vector3.Angle(Vector3.up, flat.Normal(Vector2.zero)), Is.LessThan(0.01f));
            var normal = slope.Normal(Vector2.zero);
            Assert.AreEqual(Mathf.Atan(0.5f) * Mathf.Rad2Deg, Vector3.Angle(Vector3.up, normal), 0.1f);
            Assert.Less(normal.x, 0f, "leans away from the rising side");
            Assert.AreEqual(0f, normal.z, 1e-4f);
        }

        [Test]
        public void Pose_ClampsTheTiltBuriesAndMirrorsAcrossX()
        {
            var heights = Heights(p => 0.4f * Mathf.Abs(p.x) + 0.1f * p.y);
            var entry = new MapPaletteEntry { key = "Rock", size = new Vector2(2f, 2f), maxTilt = 10f, buriedDepth = 0.3f };
            var left = MapPlacementPose.OnGround(new MapPlacement(entry, new Vector2(-20f, 5f), 30f, 1f), heights, 0f);
            var right = MapPlacementPose.OnGround(new MapPlacement(entry, new Vector2(20f, 5f), -30f, 1f), heights, 0f);

            Assert.AreEqual(10f, Vector3.Angle(Vector3.up, left.rotation * Vector3.up), 0.1f, "steeper ground clamps to the limit");
            Assert.AreEqual(heights.Height(new Vector2(-20f, 5f)) - 0.3f, left.position.y, 1e-4f);
            var mirror = new Vector3(-1f, 1f, 1f);
            foreach (var axis in new[] { Vector3.forward, Vector3.right, Vector3.up })
            {
                var expected = Vector3.Scale(mirror, left.rotation * Vector3.Scale(mirror, axis));
                Assert.That(Vector3.Distance(expected, right.rotation * axis), Is.LessThan(1e-3f), $"mirrored {axis}");
            }
        }

        [Test]
        public void Pose_StaysUprightWithoutMaxTilt()
        {
            var heights = Heights(p => 0.5f * p.x);
            var entry = new MapPaletteEntry { key = "House", size = new Vector2(8f, 8f) };
            var pose = MapPlacementPose.OnGround(new MapPlacement(entry, Vector2.zero, 0f, 1f), heights, 0f);

            Assert.That(Vector3.Angle(Vector3.up, pose.rotation * Vector3.up), Is.LessThan(0.01f));
        }

        [Test]
        public void GrassNearProps_IsNoneOnTheFootprintAndFullPastTheFalloff()
        {
            var climate = MapRoadFixture.Climate();
            var layout = MapRoadFixture.Layout(MapRoadFixture.Spec());
            var clearance = new MapClearance(layout, MapRoadFixture.Shapes(layout, climate), 15f);
            clearance.Reserve(new Vector2(10f, 10f), 2f);

            Assert.AreEqual(0f, MapDetailPainter.GrassNearProps(clearance, new Vector2(11.9f, 10f)));
            Assert.AreEqual(1f, MapDetailPainter.GrassNearProps(clearance, new Vector2(10f, 12f + MapDetailPainter.PropGrassFalloff + 0.01f)));
            var halfway = MapDetailPainter.GrassNearProps(clearance, new Vector2(12f + MapDetailPainter.PropGrassFalloff * 0.5f, 10f));
            Assert.AreEqual(0.5f, halfway, 0.01f);
        }

        [Test]
        public void RoadsideSpot_SitsOffTheNearestRoadOnTheSpotsSideFacingAlongIt()
        {
            var climate = MapRoadFixture.Climate();
            var layout = MapRoadFixture.Layout(MapRoadFixture.Spec());
            var shapes = MapRoadFixture.Shapes(layout, climate);
            shapes.AddRoad(MapRoadFixture.RoadShape(climate.RoadClass("Asphalt"), false,
                new Vector2(-100f, -50f), new Vector2(100f, -50f)));

            Assert.IsTrue(shapes.RoadsideSpot(new Vector2(10f, -45f), 2f, out var north, out var yaw));
            Assert.AreEqual(new Vector2(10f, -44f), north);
            Assert.AreEqual(90f, Mathf.Abs(Mathf.DeltaAngle(0f, yaw)), 0.01f);
            Assert.IsTrue(shapes.RoadsideSpot(new Vector2(10f, -53f), 2f, out var south, out _));
            Assert.AreEqual(new Vector2(10f, -56f), south);
            Assert.IsFalse(shapes.RoadsideSpot(new Vector2(10f, 40f), 2f, out _, out _), "no road reaches it");
        }

        [Test]
        public void SurfaceTrees_WithMaxEdgeDistance_GrowOnlyNearTheEdge()
        {
            var climate = MapRoadFixture.Climate();
            var shrub = new GameObject("Shrub");
            climate.trees.Add(new MapClimateTree { key = "Shrub", prefabs = { shrub } });
            var rough = MapClimateFixture.Surface("Rough", "Meadow");
            rough.treeSpacing = 1.5f;
            rough.trees.Add(new MapTreeDensity { tree = "Shrub", perHectare = 400f, maxEdgeDistance = 4f });
            climate.surfaces.Add(rough);
            var issues = new MapIssues();
            var context = MapRoadFixture.Context(MapRoadFixture.Spec(), climate, issues);
            var area = new Rect(-60f, -80f, 80f, 60f);
            context.Land.ClaimWhere(area, context.Region(MapLandKind.Margin, "Rough"), _ => true);

            new MapSurfacePlanter(context).Plan();
            Object.DestroyImmediate(shrub);

            var inArea = context.Trees.Planted.Where(t => area.Contains(t.Position)).ToList();
            Assert.Greater(inArea.Count, 20);
            foreach (var tree in inArea)
            {
                var p = tree.Position;
                var edge = Mathf.Min(Mathf.Min(p.x - area.xMin, area.xMax - p.x), Mathf.Min(p.y - area.yMin, area.yMax - p.y));
                Assert.LessOrEqual(edge, 5f, $"tree at {p} is {edge} m from the edge");
            }
        }

        [Test]
        public void SurfaceProps_InTheBackdrop_AreSceneryAndInThePlayAreaPlacements()
        {
            var climate = MapRoadFixture.Climate();
            var rough = MapClimateFixture.Surface("Rough", "Meadow");
            rough.props.Add(new MapPropDensity { kind = "Stone", perHectare = 60f });
            climate.surfaces.Add(rough);
            var (prefab, model) = (new GameObject("Stone"), new GameObject("StoneModel"));
            var issues = new MapIssues();
            var context = MapRoadFixture.Context(MapRoadFixture.Spec(), climate, issues);
            context.Palette.entries.Add(new MapPaletteEntry { key = "Stone", prefab = prefab, scenery = model, size = Vector2.one });
            context.Land.ClaimWhere(context.Land.Bounds, context.Region(MapLandKind.Margin, "Rough"), _ => true);

            new MapSurfacePlanter(context).Plan();
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(model);

            var playArea = context.Layout.Bounds;
            Assert.Greater(context.Scenery.Count, 0);
            Assert.IsTrue(context.Scenery.All(s => !playArea.Contains(s.Position) && s.Model == model));
            var stones = context.Placements.All.Where(p => p.Entry.key == "Stone").ToList();
            Assert.Greater(stones.Count, 0);
            Assert.IsTrue(stones.All(p => playArea.Contains(p.Position)));
        }

        private static MapHeights Heights(System.Func<Vector2, float> height)
        {
            var heights = new MapHeights(129, new Rect(-64f, -64f, 128f, 128f));
            for (var z = 0; z < heights.Resolution; z++)
            {
                for (var x = 0; x < heights.Resolution; x++)
                {
                    heights[x, z] = height(heights.Position(x, z));
                }
            }

            return heights;
        }
    }
}
