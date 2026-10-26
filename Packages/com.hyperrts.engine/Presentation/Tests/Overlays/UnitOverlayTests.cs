using HyperRTS.Presentation.Overlays;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Overlays
{
    /// <summary>Ground marker shapes and poses, the select-in animation and which units show a health bar.</summary>
    public class UnitOverlayTests
    {
        private const float Margin = 0.35f;

        private OverlayStyle _style;

        [SetUp]
        public void SetUp() => _style = ScriptableObject.CreateInstance<OverlayStyle>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_style);

        [Test]
        public void VehicleHull_IsRoundedRectangleAsLongAsTheHull()
        {
            var truck = new NavAgent { Radius = 1.3f, HullLength = 9f };

            var footprint = MarkerFootprint.Agent(truck, Margin);

            Assert.AreEqual(MarkerShape.Outline, footprint.Shape);
            Assert.AreEqual(1.3f + Margin, footprint.HalfSize.x, 1e-4f);
            Assert.AreEqual(4.5f + Margin, footprint.HalfSize.y, 1e-4f);
            Assert.Less(footprint.CornerRadius, footprint.HalfSize.x);
        }

        [Test]
        public void RoundAgent_IsCircle()
        {
            var squad = new NavAgent { Radius = 1.5f, HullLength = 0f };

            var footprint = MarkerFootprint.Agent(squad, Margin);

            Assert.AreEqual(footprint.HalfSize.x, footprint.HalfSize.y, 1e-4f);
            Assert.AreEqual(footprint.HalfSize.x, footprint.CornerRadius, 1e-4f);
        }

        [Test]
        public void Building_GetsBracketsRoundItsBox()
        {
            var footprint = MarkerFootprint.Building(new NavObstacle { Size = new float2(12f, 8f) }, Margin);

            Assert.AreEqual(MarkerShape.Brackets, footprint.Shape);
            Assert.AreEqual(6f + Margin, footprint.HalfSize.x, 1e-4f);
            Assert.AreEqual(4f + Margin, footprint.HalfSize.y, 1e-4f);
        }

        [Test]
        public void Unit_AircraftTakesItsAgentsLayerAndRadius()
        {
            var unit = new OverlayUnit(new NavAgent { Radius = 2f, Layer = NavLayer.Air }, null, 3, default);

            Assert.AreEqual(NavLayer.Air, unit.Layer);
            Assert.AreEqual(2f, unit.Radius, 1e-4f);
            Assert.AreEqual(2f + Margin, unit.Footprint(Margin).HalfSize.x, 1e-4f);
            Assert.IsFalse(unit.IsSquad);
            Assert.AreEqual(0, unit.Members);
        }

        [Test]
        public void Unit_BuildingIsGroundWithBracketsAndHalfItsLongestSide()
        {
            var unit = new OverlayUnit(null, new NavObstacle { Size = new float2(12f, 8f) }, 0, default);

            Assert.AreEqual(NavLayer.Ground, unit.Layer);
            Assert.AreEqual(6f, unit.Radius, 1e-4f);
            Assert.AreEqual(MarkerShape.Brackets, unit.Footprint(Margin).Shape);
        }

        [Test]
        public void Unit_WithNeitherAgentNorObstacleIsDefaultCircle()
        {
            var unit = new OverlayUnit(null, null, 0, default);

            Assert.AreEqual(EntityRadius.Default, unit.Radius, 1e-4f);
            Assert.AreEqual(MarkerShape.Outline, unit.Footprint(Margin).Shape);
        }

        [Test]
        public void Pose_TurnedBuildingTurnsItsBracketsLikeItsNavBox()
        {
            var turn = quaternion.RotateY(math.PI / 2f);
            var world = new LocalToWorld { Value = float4x4.TRS(float3.zero, turn, 1f) };

            var pose = new MarkerPose(world, NavLayer.Ground);
            var box = new NavBox(float3.zero, turn, new float2(12f, 8f));
            var right = math.mul(pose.Facing, math.right());

            Assert.AreEqual(box.Right.x, right.x, 1e-3f);
            Assert.AreEqual(box.Right.y, right.z, 1e-3f);
        }

        [Test]
        public void Pose_ReadsTheDrawnTransformAndFlattensItsFacing()
        {
            var tilted = quaternion.Euler(0.3f, math.PI / 2f, 0f);
            var world = new LocalToWorld { Value = float4x4.TRS(new float3(5f, 2f, -3f), tilted, 1f) };

            var pose = new MarkerPose(world, NavLayer.Ground);
            var forward = math.mul(pose.Facing, math.forward());

            Assert.AreEqual(new float3(5f, 2f, -3f), pose.Position);
            Assert.AreEqual(0f, forward.y, 1e-4f);
            Assert.AreEqual(1f, forward.x, 1e-3f);
        }

        [Test]
        public void Pose_AircraftMarkTheGroundAndBoatsKeepTheSurface()
        {
            var world = new LocalToWorld { Value = float4x4.Translate(new float3(0f, 40f, 0f)) };

            var air = new MarkerPose(world, NavLayer.Air);
            var naval = new MarkerPose(world, NavLayer.Naval);

            Assert.Less(air.Floor, -1000f);
            Assert.Greater(naval.Floor, 35f);
            Assert.AreEqual(naval.Floor, naval.Matrix(MarkerFootprint.Round(1f, 0f)).m13, 1e-4f);
        }

        [Test]
        public void SelectIn_StartsLargeAndFadedThenSettles()
        {
            var start = SelectionFades.ScaleAndAlpha(0f, 0.15f, 1.2f);
            var end = SelectionFades.ScaleAndAlpha(0.15f, 0.15f, 1.2f);

            Assert.AreEqual(1.2f, start.x, 1e-4f);
            Assert.AreEqual(0f, start.y, 1e-4f);
            Assert.AreEqual(1f, end.x, 1e-4f);
            Assert.AreEqual(1f, end.y, 1e-4f);
        }

        [Test]
        public void SelectionFades_RestartAfterDeselection()
        {
            var fades = new SelectionFades();
            var unit = new Entity { Index = 7, Version = 1 };

            fades.Elapsed(unit, 1f);
            fades.EndFrame();
            var kept = fades.Elapsed(unit, 2f);
            fades.EndFrame();
            fades.EndFrame();
            var restarted = fades.Elapsed(unit, 5f);

            Assert.AreEqual(1f, kept, 1e-4f);
            Assert.AreEqual(0f, restarted, 1e-4f);
        }

        [TestCase(HealthBarVisibility.Always, false, false, false, true)]
        [TestCase(HealthBarVisibility.DamagedAndSelected, false, false, true, true)]
        [TestCase(HealthBarVisibility.DamagedAndSelected, false, false, false, false)]
        [TestCase(HealthBarVisibility.SelectedOnly, false, false, true, false)]
        [TestCase(HealthBarVisibility.SelectedOnly, true, false, false, true)]
        [TestCase(HealthBarVisibility.SelectedOnly, false, true, false, true)]
        public void HealthBar_FollowsTheVisibilitySetting(HealthBarVisibility visibility, bool selected, bool hovered,
            bool damaged, bool shown)
        {
            var focus = new OverlayFocus(selected, hovered, damaged);

            Assert.AreEqual(shown, focus.ShowsBar(visibility));
        }

        [Test]
        public void HealthBar_SegmentsPerHealthChunkOrSquadMember()
        {
            var tank = new HealthBarData(new Health { Current = 300f, Max = 600f }, 0, 0, 2, 2f, _style);
            var squad = new HealthBarData(new Health { Current = 50f, Max = 100f }, 5, 3, 0, 1f, _style);
            var fortress = new HealthBarData(new Health { Current = 1f, Max = 50000f }, 0, 0, 0, 10f, _style);

            Assert.AreEqual(6, tank.Segments);
            Assert.AreEqual(0.5f, tank.Fraction, 1e-4f);
            Assert.AreEqual(2, tank.Rank);
            Assert.AreEqual(5, squad.Segments);
            Assert.AreEqual(_style.maxSegments, fortress.Segments);
            Assert.AreEqual(_style.barWidthRange.y, fortress.Width, 1e-4f);
        }
    }
}
