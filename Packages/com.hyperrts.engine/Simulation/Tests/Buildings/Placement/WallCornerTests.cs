using System;
using HyperRTS.Simulation.Buildings.Placement;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Buildings.Placement
{
    /// <summary>A wall drawn with corners: each leg joined to the last starts against it, never inside it.</summary>
    public class WallCornerTests : WallFixture
    {
        [TestCase(90f)]
        [TestCase(45f)]
        [TestCase(-60f)]
        public void JoinedLeg_TurnsTheCorner_TouchingTheLastSegmentWithoutOverlap(float degrees)
        {
            var prefab = WallPrefab();
            SetBuildingGap(4f);
            var direction = new float2(math.cos(math.radians(degrees)), math.sin(math.radians(degrees)));
            DrawCorner(Builder(prefab, float3.zero), prefab, direction, 6f);

            var boxes = SiteBoxes();
            Assert.AreEqual(8, boxes.Length, "every segment of both legs, the gap kept to buildings only");
            AssertOnlyTouch(boxes);
            var corner = Leg().JoinedStart(direction).xz;
            var turned = Array.Find(boxes, box => math.distance(box.Center, corner) < 1e-3f);
            var gap = Leg().Segment(3).Box.Gap(turned);
            Assert.AreEqual(0f, gap, BuildingSpot.FlushTolerance, "the corner stays closed");
        }

        [Test]
        public void JoinedLeg_AtARightAngle_StartsJustPastTheLastSegment()
        {
            MathAssert.Near(new float3(11f, 0f, 2.1f), Leg().JoinedStart(new float2(0f, 1f)));
        }

        [Test]
        public void JoinedLeg_StraightOn_ContinuesFlush()
        {
            MathAssert.Near(new float3(12f, 0f, 0.5f), Leg().JoinedStart(new float2(1f, 0f)));
        }
    }
}
