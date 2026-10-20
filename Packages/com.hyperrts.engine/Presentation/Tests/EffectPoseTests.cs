using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Effect poses: facing a direction, and impacts laid on flat or sloped ground.</summary>
    public class EffectPoseTests
    {
        private const float Spin = 0.7f;
        private static readonly Vector3 RampNormal = new Vector3(-1f, 1f, 0f).normalized;

        private TerrainHeight _ramp;

        // A 45 degree ramp rising along x: height = x.
        [SetUp]
        public void SetUp()
        {
            using var heights = new NativeArray<float>(new[] { 0f, 10f, 0f, 10f }, Allocator.Temp);
            _ramp = TerrainHeight.Create(heights, new int2(2, 2), float2.zero, 10f, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown() => _ramp.Blob.Dispose();

        [Test]
        public void Facing_UpASlope_LooksAlongIt_WithoutRoll()
        {
            var direction = new Vector3(1f, 1f, 0f);

            var facing = EffectPose.Facing(direction);

            MathAssert.Near(direction.normalized, facing * Vector3.forward);
            Assert.AreEqual(0f, (facing * Vector3.right).y, 1e-5f, "kept upright");
            Assert.Greater((facing * Vector3.up).y, 0f);
        }

        [Test]
        public void Facing_ZeroDirection_IsUnturned()
        {
            Assert.AreEqual(Quaternion.identity, EffectPose.Facing(Vector3.zero));
        }

        [Test]
        public void GroundImpact_Aligned_SitsOnTheSlope_UpAlongItsNormal()
        {
            var (position, rotation) = EffectPose.Impact(Hit(ImpactSurface.Ground), _ramp, true, true, Spin);

            MathAssert.Near(new Vector3(5f, 5f, 5f), position);
            MathAssert.Near(RampNormal, rotation * Vector3.up);
        }

        [Test]
        public void GroundImpact_Unaligned_SitsOnTheSlope_UpStaysWorldUp()
        {
            var (position, rotation) = EffectPose.Impact(Hit(ImpactSurface.Ground), _ramp, true, false, Spin);

            MathAssert.Near(new Vector3(5f, 5f, 5f), position);
            MathAssert.Near(Vector3.up, rotation * Vector3.up);
        }

        [Test]
        public void OffTheGround_KeepsTheCuePosition_AndOnlySpins()
        {
            var spin = Quaternion.Euler(0f, Spin * Mathf.Rad2Deg, 0f);

            var air = EffectPose.Impact(Hit(ImpactSurface.Air), _ramp, true, true, Spin);
            var noTerrain = EffectPose.Impact(Hit(ImpactSurface.Ground), _ramp, false, true, Spin);

            MathAssert.Near(new Vector3(5f, 9f, 5f), air.Position);
            MathAssert.Near(new Vector3(5f, 9f, 5f), noTerrain.Position);
            Assert.Greater(Quaternion.Dot(spin, air.Rotation), 0.9999f);
            Assert.Greater(Quaternion.Dot(spin, noTerrain.Rotation), 0.9999f);
        }

        private static CueEvent Hit(ImpactSurface surface) =>
            new() { Slot = CueSlot.Impact, Position = new float3(5f, 9f, 5f), Surface = surface };
    }
}
