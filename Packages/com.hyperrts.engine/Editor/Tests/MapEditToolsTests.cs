using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Designer;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>The Map Editor's drawing tools: snapping, vertex rules, the toolbox and adding features.</summary>
    public class MapEditToolsTests
    {
        [Test]
        public void Grid_SnapsToHalfMetres()
        {
            Assert.AreEqual(new Vector2(1.5f, -2f), MapSnap.Grid(new Vector2(1.3f, -2.2f)));
        }

        [TestCase(MapSymmetry.MirrorXName, 0f, 7.5f)]
        [TestCase(MapSymmetry.MirrorZName, 3f, 0f)]
        [TestCase(MapSymmetry.Rotate180, 0f, 0f)]
        [TestCase("rotate-4", 0f, 0f)]
        public void SharedPositions_SnapOntoWhatEveryCopyLeavesInPlace(string symmetry, float x, float z)
        {
            var parsed = MapSymmetry.Parse(symmetry);

            var placed = MapSnap.Position(new Vector2(3.1f, 7.4f), true, parsed);

            Assert.AreEqual(new Vector2(x, z), placed);
            Assert.IsTrue(parsed.IsFixed(new[] { placed }), "a shared point there stays symmetric");
        }

        [Test]
        public void UnsharedPositions_OnlySnapToTheGrid()
        {
            var placed = MapSnap.Position(new Vector2(3.1f, 7.4f), false, MapSymmetry.Parse(MapSymmetry.Rotate180));

            Assert.AreEqual(new Vector2(3f, 7.5f), placed);
        }

        [Test]
        public void SegmentAt_FindsTheNearestSegmentWithinReach()
        {
            var line = new List<Vector2> { new(0f, 0f), new(10f, 0f), new(10f, 10f) };

            Assert.AreEqual(0, MapVertexRules.SegmentAt(line, false, new Vector2(5f, 1f), 2f));
            Assert.AreEqual(1, MapVertexRules.SegmentAt(line, false, new Vector2(9f, 5f), 2f));
            Assert.AreEqual(-1, MapVertexRules.SegmentAt(line, false, new Vector2(5f, 5f), 2f), "too far from any");
            Assert.AreEqual(-1, MapVertexRules.SegmentAt(line, false, new Vector2(5f, 5.5f), 2f), "open line: no closing edge");
            Assert.AreEqual(2, MapVertexRules.SegmentAt(line, true, new Vector2(5f, 5.5f), 2f), "ring: the closing edge");
        }

        [Test]
        public void InsertIndex_GoesAfterTheSegmentsFirstPoint()
        {
            Assert.AreEqual(1, MapVertexRules.InsertIndex(0));
            Assert.AreEqual(3, MapVertexRules.InsertIndex(2), "a ring's closing segment appends");
        }

        [Test]
        public void CanRemove_KeepsTwoLinePointsAndThreeAreaPoints()
        {
            Assert.IsTrue(MapVertexRules.CanRemove(MapFeatureGeometry.Polyline, 3));
            Assert.IsFalse(MapVertexRules.CanRemove(MapFeatureGeometry.Polyline, 2));
            Assert.IsTrue(MapVertexRules.CanRemove(MapFeatureGeometry.Polygon, 4));
            Assert.IsFalse(MapVertexRules.CanRemove(MapFeatureGeometry.Polygon, 3));
            Assert.IsFalse(MapVertexRules.CanRemove(MapFeatureGeometry.Point, 1));
            Assert.IsFalse(MapVertexRules.CanRemove(MapFeatureGeometry.Circle, 1));
        }

        [Test]
        public void Toolbox_OffersEachToolTheKindsOfItsGeometry()
        {
            Assert.IsEmpty(MapToolbox.KindsFor(MapToolMode.Select));
            CollectionAssert.Contains(MapToolbox.KindsFor(MapToolMode.Point).Select(k => k.Field).ToList(), "hills");
            CollectionAssert.Contains(MapToolbox.KindsFor(MapToolMode.Line).Select(k => k.Field).ToList(), "roads");
            CollectionAssert.Contains(MapToolbox.KindsFor(MapToolMode.Area).Select(k => k.Field).ToList(), "woods");

            var toolbox = new MapToolbox();
            toolbox.SetMode(MapToolMode.Line);
            Assert.Throws<System.ArgumentException>(() => toolbox.SetKind(MapFeatureKinds.Find("woods")));
        }

        [Test]
        public void Add_CopiesTheLastFeatureButNotItsPositionOrCrossings()
        {
            var spec = new MapSpec();
            spec.rivers.Add(new MapRiverSpec { width = 9f, points = { new MapPoint(0f, 0f), new MapPoint(5f, 0f) } });
            spec.rivers[0].crossings.Add(new MapCrossingSpec { kind = MapCrossingSpec.Ford });
            var rivers = MapFeatureKinds.Find("rivers");

            var index = rivers.Add(spec, new[] { new Vector2(1f, 1f), new Vector2(2f, 2f), new Vector2(3f, 1f) });

            Assert.AreEqual(1, index);
            Assert.AreEqual(9f, spec.rivers[1].width);
            Assert.AreEqual(3, spec.rivers[1].points.Count);
            Assert.IsEmpty(spec.rivers[1].crossings);
            Assert.AreEqual(2, spec.rivers[0].points.Count, "the copied river keeps its own points");
        }

        [Test]
        public void Add_ToAnEmptyList_TakesTheTypeDefaults()
        {
            var spec = new MapSpec();
            var hills = MapFeatureKinds.Find("hills");

            hills.Add(spec, new[] { new Vector2(4f, -6f) });

            Assert.AreEqual(new MapPoint(4f, -6f), spec.hills[0].pos);
            Assert.AreEqual(new MapHillSpec().radius, spec.hills[0].radius);
        }
    }
}
