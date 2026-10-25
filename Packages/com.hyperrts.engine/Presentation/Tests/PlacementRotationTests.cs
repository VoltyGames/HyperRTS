using HyperRTS.Input.Commands;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The ghost's yaw: drag aim, a held turn, a tap and the 15° snap.</summary>
    public class PlacementRotationTests
    {
        [Test]
        public void Start_FacesTheCamera()
        {
            Assert.AreEqual(PlacementMath.CameraYaw, PlacementRotation.Start().Yaw(false));
        }

        [Test]
        public void Aim_PointsTheFrontAlongThePull()
        {
            var rotation = PlacementRotation.Start();
            rotation.Aim(new float2(1f, 1f));

            var yaw = rotation.Yaw(false);
            Assert.AreEqual(math.SQRT2 * 0.5f, math.sin(yaw), 1e-5f);
            Assert.AreEqual(math.SQRT2 * 0.5f, math.cos(yaw), 1e-5f);
        }

        [Test]
        public void Tap_AddsFortyFiveDegrees()
        {
            var rotation = PlacementRotation.Start();
            rotation.Press(100f);
            rotation.Turn(102f);
            rotation.Release();

            Assert.AreEqual(PlacementMath.CameraYaw + math.radians(45f), rotation.Yaw(false), 1e-5f);
            Assert.IsFalse(rotation.Turning);
        }

        [Test]
        public void HeldTurn_FollowsThePointer_AndSnapsToFifteenDegrees()
        {
            var rotation = PlacementRotation.Start();
            rotation.Press(100f);
            rotation.Turn(120f);
            rotation.Turn(140f);

            Assert.IsTrue(rotation.Turning);
            Assert.AreEqual(PlacementMath.CameraYaw + math.radians(20f), rotation.Yaw(false), 1e-5f, "0.5° a pixel");
            Assert.AreEqual(PlacementMath.CameraYaw + math.radians(15f), rotation.Yaw(true), 1e-5f);
            rotation.Release();
            Assert.AreEqual(PlacementMath.CameraYaw + math.radians(20f), rotation.Yaw(false), 1e-5f, "a turn is no tap");
        }

        [Test]
        public void SnapEnd_TurnsTheLineToAFifteenDegreeStep_KeepingItsLength()
        {
            var start = new float3(1f, 0f, 1f);
            var end = PlacementRotation.SnapEnd(start, start + new float3(10f, 2f, 4f));

            var delta = end.xz - start.xz;
            Assert.AreEqual(math.length(new float2(10f, 4f)), math.length(delta), 1e-4f);
            Assert.AreEqual(15f, math.degrees(math.atan2(delta.y, delta.x)), 1e-3f);
            Assert.AreEqual(2f, end.y);
        }
    }
}
