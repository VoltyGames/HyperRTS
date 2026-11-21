using HyperRTS.Presentation.Overlays;
using HyperRTS.Presentation.Overlays.Bars;
using HyperRTS.Presentation.Overlays.Markers;
using HyperRTS.Presentation.Overlays.Pips;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Overlays
{
    /// <summary>
    /// Ground marker shapes and poses, the select-in animation, which units show a health bar or a small-unit pip,
    /// and the bars' smallest size.
    /// </summary>
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
        public void Unit_BuildingIsGroundWithBracketsAndHalfItsLongestSide()
        {
            var unit = new OverlayUnit(null, new NavObstacle { Size = new float2(12f, 8f) }, 0, default, null, false);

            Assert.AreEqual(NavLayer.Ground, unit.Layer);
            Assert.AreEqual(6f, unit.Radius, 1e-4f);
            Assert.AreEqual(MarkerShape.Brackets, unit.Footprint(Margin).Shape);
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
            var tank = new HealthBarData(new Health { Current = 300f, Max = 600f }, 2, 2f, SquadBarState.None, _style);
            var fivePips = new SquadBarState(-1f, SuppressionLevel.Steady, CoverKind.None, false, 5, 3);
            var squad = new HealthBarData(new Health { Current = 50f, Max = 100f }, 0, 1f, fivePips, _style);
            var fortress = new HealthBarData(new Health { Current = 1f, Max = 50000f }, 0, 10f, SquadBarState.None,
                _style);

            Assert.AreEqual(6, tank.Segments);
            Assert.AreEqual(0.5f, tank.Fraction, 1e-4f);
            Assert.AreEqual(2, tank.Rank);
            Assert.AreEqual(5, squad.Segments);
            Assert.AreEqual(_style.maxSegments, fortress.Segments);
            Assert.AreEqual(_style.barWidthRange.y, fortress.Width, 1e-4f);
        }

        [Test]
        public void Pip_ShowsOnceASoldierShrinksBelowTheThresholdWithDistanceOrASteepView()
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.aspect = 16f / 9f;
            var head = Vector3.up * 1.8f;

            var near = new ReferencePixels(camera).Projected(new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, 30f) + head);
            var far = new ReferencePixels(camera).Projected(new Vector3(0f, 0f, 300f), new Vector3(0f, 0f, 300f) + head);
            camera.transform.SetPositionAndRotation(new Vector3(0f, 26f, 15f), Quaternion.Euler(60f, 0f, 0f));
            var steep = new ReferencePixels(camera).Projected(new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, 30f) + head);
            Object.DestroyImmediate(camera.gameObject);

            Assert.AreEqual(near / 10f, far, 0.05f);
            Assert.Less(steep, near * 0.6f);
            Assert.IsFalse(UnitPips.Shows(near, _style));
            Assert.IsTrue(UnitPips.Shows(far, _style));
        }

        [Test]
        public void HealthBar_KeepsItsMinimumHeightOnSmallScreensAndScales()
        {
            var fullHD = HealthBars.PixelScale(1080f, 1f, _style);
            var small = HealthBars.PixelScale(540f, 0.8f, _style);

            Assert.AreEqual(1f, fullHD, 1e-4f);
            Assert.AreEqual(_style.barMinHeight, _style.barHeight * small, 1e-4f);
        }
    }
}
