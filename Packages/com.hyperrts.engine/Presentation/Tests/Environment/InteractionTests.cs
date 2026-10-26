using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HyperRTS.Presentation.Environment;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Tests;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Tests.Environment
{
    /// <summary>Interaction maps on the CPU: texture sizing, fades, and the stamps vehicles, craters, wakes, feet and gusts lay.</summary>
    public class InteractionTests
    {
        private const float Reach = 25f;

        private static readonly TrackMarks Tank = new() { Width = 0.6f, Gauge = 2.8f, RutDepth = 1f };

        private ImpactMark _mark;

        [SetUp]
        public void SetUp() => _mark = ScriptableObject.CreateInstance<ImpactMark>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_mark);

        [TestCase(500f, 301f, 2500, 1505)]
        [TestCase(4000f, 100f, InteractionMath.MaxTextureSize, 500)]
        public void TextureSize_CoversTheMapAtTheTexelSize_CappingHugeMaps(float x, float z, int width, int height) =>
            Assert.AreEqual(new int2(width, height), InteractionMath.TextureSize(new float2(x, z), 0.2f));

        [Test]
        public void Texels_LowIsTwiceAsCoarse()
        {
            var tracks = InteractionMath.TracksTexel(InteractionQuality.Full);
            var trample = InteractionMath.TrampleTexel(InteractionQuality.Full);
            Assert.AreEqual(2f * tracks, InteractionMath.TracksTexel(InteractionQuality.Low), 1e-6f);
            Assert.AreEqual(2f * trample, InteractionMath.TrampleTexel(InteractionQuality.Low), 1e-6f);
            Assert.Less(tracks, trample, "ruts need finer texels than flattened grass");
            Assert.Throws<ArgumentOutOfRangeException>(() => InteractionMath.TrampleTexel(InteractionQuality.Off));
        }

        [Test]
        public void WorldToUV_MapsTheRectCornersToZeroAndOne()
        {
            var rect = InteractionMath.Rect(new float2(-100f, 20f), new float2(400f, 200f));

            MathAssert.Near(new float2(0f, 0f), InteractionMath.WorldToUV(rect, new float2(-100f, 20f)));
            MathAssert.Near(new float2(1f, 1f), InteractionMath.WorldToUV(rect, new float2(300f, 220f)));
            MathAssert.Near(new float2(0.25f, 0.5f), InteractionMath.WorldToUV(rect, new float2(0f, 120f)));
        }

        [Test]
        public void FadeSteps_EmptyAChannelInItsDuration_CarryingFractions()
        {
            var carry = 0f;
            var steps = 0;
            for (var frame = 0; frame < 60 * 60; frame++)
            {
                steps += InteractionMath.FadeSteps(ref carry, 1f / 60f, InteractionMath.TrampleRecoverySeconds);
            }

            Assert.That(steps, Is.InRange(InteractionMath.Levels - 1, InteractionMath.Levels));

            carry = 0f;
            Assert.AreEqual(0, InteractionMath.FadeSteps(ref carry, 0.5f, InteractionMath.TrackFadeSeconds));
            Assert.AreEqual(1, InteractionMath.FadeSteps(ref carry, 0.5f, InteractionMath.TrackFadeSeconds));
            Assert.AreEqual(0f, carry, 0.07f);
        }

        [Test]
        public void AddVehicle_PutsARutUnderEachTrack_AndFlattensItsWholeWidth()
        {
            var trample = new List<InteractionStamp>();
            var tracks = new List<InteractionStamp>();

            InteractionMath.AddVehicle(Tank, new float2(10f, 20f), new float2(0f, 1f), 0f, trample, tracks);

            Assert.AreEqual(2, tracks.Count);
            Assert.AreEqual(8.6f, tracks[0].Center.x, 1e-5f);
            Assert.AreEqual(11.4f, tracks[1].Center.x, 1e-5f);
            MathAssert.Near(new float2(0.3f, 0.3f), tracks[0].HalfSize);
            Assert.AreEqual(1f, tracks[0].Value.x);
            Assert.AreEqual(1, trample.Count);
            Assert.AreEqual(1.7f, trample[0].HalfSize.x, 1e-5f);
            Assert.AreEqual(new float4(1f, 1f, 1f, 0f), trample[0].Value);
        }

        [TestCase(2f, 9f, TestName = "Driving forward reaches back")]
        [TestCase(-2f, 11f, TestName = "Reversing reaches forward")]
        public void AddVehicle_CoversTheDistanceDriven(float driven, float center)
        {
            var tracks = new List<InteractionStamp>();

            InteractionMath.AddVehicle(Tank, new float2(0f, 10f), new float2(0f, 1f), driven,
                new List<InteractionStamp>(), tracks);

            Assert.AreEqual(center, tracks[0].Center.y, 1e-5f);
            Assert.AreEqual(1.3f, tracks[0].HalfSize.y, 1e-5f);
        }

        [Test]
        public void Footprint_FlattensForGoodWithoutSteering()
        {
            var stamp = InteractionMath.Footprint(new float2(5f, 6f), new float2(12f, 8f));

            Assert.AreEqual(new float2(6f, 4f), stamp.HalfSize);
            Assert.AreEqual(new float4(1f, 1f, 0f, 0f), stamp.Value);
        }

        [Test]
        public void Footsteps_CoverTheStrideBehindTheFeet_AndOnlyBendPlants()
        {
            var stamp = InteractionMath.Footsteps(new float2(10f, 0f), new float2(1f, 0f), 0.4f);

            Assert.AreEqual(9.8f, stamp.Center.x, 1e-5f);
            Assert.AreEqual(InteractionMath.FootWidth + 0.2f, stamp.HalfSize.y, 1e-5f);
            Assert.Less(stamp.Value.x, 1f, "leans plants without flattening them");
        }

        [Test]
        public void Stamp_StrideMatchesTheStruct() =>
            Assert.AreEqual(Marshal.SizeOf<InteractionStamp>(), InteractionStamp.Stride);

        [Test]
        public void AddCrater_BowlIsHalfTheMarkInsideAWiderScorchDisc()
        {
            _mark.craterDepth = 0.8f;
            _mark.scorch = 0.6f;
            var stamps = new InteractionStamps();

            stamps.AddCrater(new float2(1f, 2f), _mark, 4f);

            var scar = stamps.Scars[0];
            Assert.AreEqual(new float2(2f * InteractionMath.ScorchReach), scar.HalfSize);
            Assert.AreEqual(0.8f / InteractionMath.MaxCraterDepth, scar.Value.x, 1e-5f);
            Assert.AreEqual(0.6f, scar.Value.y, 1e-5f);
            Assert.AreEqual(1f / InteractionMath.ScorchReach, scar.Value.z, 1e-5f, "bowl share of the stamp");
            Assert.AreEqual(scar.HalfSize, stamps.Trample[0].HalfSize, "plants flattened over the whole scar");
            Assert.AreEqual(1f, stamps.Trample[0].Value.w, "round");
            Assert.IsEmpty(stamps.Tracks, "scars stay out of the fading channels' stamps");
        }

        [Test]
        public void AddCrater_WithoutScorch_IsJustTheBowl_AndDeepCratersClip()
        {
            _mark.craterDepth = 5f;
            var stamps = new InteractionStamps();

            stamps.AddCrater(float2.zero, _mark, 6f);

            Assert.AreEqual(new float2(3f), stamps.Scars[0].HalfSize);
            Assert.AreEqual(new float4(1f, 0f, 1f, 1f), stamps.Scars[0].Value);
        }

        [Test]
        public void AddWake_TrailsBehindTheUnit_FoamGrowingWithSpeed()
        {
            var stamps = new InteractionStamps();

            stamps.AddWake(new float2(10f, 0f), new float2(1f, 0f), 4f, 2f);
            stamps.AddWake(float2.zero, new float2(0f, 1f), 2f * InteractionMath.FullWakeSpeed, 2f);

            var wake = stamps.Wakes[0];
            Assert.Less(wake.Center.x, 10f, "centre behind the unit");
            Assert.AreEqual(10f + 2f, wake.Center.x + wake.HalfSize.y, 1e-4f, "front edge at the unit's bow");
            Assert.AreEqual(4f / InteractionMath.FullWakeSpeed, wake.Value.x, 1e-5f);
            Assert.AreEqual(1f, stamps.Wakes[1].Value.x, "full foam caps");
        }

        [Test]
        public void Clear_EmptiesEveryListAndTheReset()
        {
            var stamps = new InteractionStamps { Reset = true };
            stamps.AddWake(float2.zero, new float2(0f, 1f), 3f, 1f);

            stamps.Clear();

            Assert.IsEmpty(stamps.Wakes);
            Assert.IsFalse(stamps.Reset);
        }

        [Test]
        public void Downwash_IsStrongestLow_AndGoneAboveItsHeight()
        {
            var low = LocalGust.Downwash(float2.zero, 3f, 1f, Reach);
            var high = LocalGust.Downwash(float2.zero, 20f, 1f, Reach);

            Assert.Greater(low.Push, high.Push);
            Assert.Greater(high.Radius, low.Radius, "spreads wider from higher up");
            Assert.AreEqual(0f, LocalGust.Downwash(float2.zero, Reach, 1f, Reach).Push);
            Assert.AreEqual(0f, LocalGust.Downwash(float2.zero, 3f, 0f, Reach).Push, "stopped rotors");
        }

        [Test]
        public void Blast_FrontSpreadsOut_WhileThePushRelaxes_WithinItsReachLimits()
        {
            var blast = LocalGust.Blast(new float2(5f, 5f), 4f);
            var start = blast;

            blast.Advance(LocalGust.BlastFront);
            Assert.AreEqual(12f, blast.Radius, 1e-4f, "three times the splash");
            Assert.Less(blast.Push, start.Push);

            blast.Advance(1f);
            Assert.IsTrue(blast.Expired);
            Assert.AreEqual(LocalGust.MinBlastReach, LocalGust.Blast(float2.zero, 1f).Reach);
            Assert.AreEqual(LocalGust.MaxBlastReach, LocalGust.Blast(float2.zero, 50f).Reach);
        }
    }
}
