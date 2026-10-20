using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Environment;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests
{
    public class InteractionMathTests
    {
        private static readonly TrackMarks Tank = new() { Width = 0.6f, Gauge = 2.8f, RutDepth = 1f };

        [Test]
        public void TextureSize_CoversTheMapAtTheTexelSize()
        {
            var size = InteractionMath.TextureSize(new float2(500f, 301f), InteractionMath.TracksTexel(InteractionQuality.Full));

            Assert.AreEqual(new int2(2500, 1505), size);
        }

        [Test]
        public void TextureSize_CapsHugeMaps()
        {
            var size = InteractionMath.TextureSize(new float2(4000f, 100f), 0.2f);

            Assert.AreEqual(new int2(InteractionMath.MaxTextureSize, 500), size);
        }

        [Test]
        public void Texels_LowIsTwiceAsCoarse()
        {
            Assert.AreEqual(0.2f, InteractionMath.TracksTexel(InteractionQuality.Full));
            Assert.AreEqual(0.4f, InteractionMath.TracksTexel(InteractionQuality.Low));
            Assert.AreEqual(1f, InteractionMath.TrampleTexel(InteractionQuality.Low));
            Assert.Throws<ArgumentOutOfRangeException>(() => InteractionMath.TrampleTexel(InteractionQuality.Off));
        }

        [Test]
        public void WorldToUV_MapsTheRectCornersToZeroAndOne()
        {
            var rect = InteractionMath.Rect(new float2(-100f, 20f), new float2(400f, 200f));

            AssertNear(new float2(0f, 0f), InteractionMath.WorldToUV(rect, new float2(-100f, 20f)));
            AssertNear(new float2(1f, 1f), InteractionMath.WorldToUV(rect, new float2(300f, 220f)));
            AssertNear(new float2(0.25f, 0.5f), InteractionMath.WorldToUV(rect, new float2(0f, 120f)));
        }

        [Test]
        public void FadeSteps_EmptiesAChannelInItsDuration()
        {
            var carry = 0f;
            var steps = 0;
            for (var frame = 0; frame < 60 * 60; frame++)
            {
                steps += InteractionMath.FadeSteps(ref carry, 1f / 60f, InteractionMath.TrampleRecoverySeconds);
            }

            Assert.That(steps, Is.InRange(InteractionMath.Levels - 1, InteractionMath.Levels));
        }

        [Test]
        public void FadeSteps_CarriesFractionsAcrossFrames()
        {
            var carry = 0f;

            Assert.AreEqual(0, InteractionMath.FadeSteps(ref carry, 0.5f, InteractionMath.TrackFadeSeconds));
            Assert.AreEqual(1, InteractionMath.FadeSteps(ref carry, 0.5f, InteractionMath.TrackFadeSeconds));
            Assert.AreEqual(0f, carry, 0.07f);
        }

        [Test]
        public void AddVehicle_PutsARutUnderEachTrack()
        {
            var trample = new List<InteractionStamp>();
            var tracks = new List<InteractionStamp>();

            InteractionMath.AddVehicle(Tank, new float2(10f, 20f), new float2(0f, 1f), 0f, trample, tracks);

            Assert.AreEqual(2, tracks.Count);
            Assert.AreEqual(8.6f, tracks[0].Center.x, 1e-5f);
            Assert.AreEqual(11.4f, tracks[1].Center.x, 1e-5f);
            AssertNear(new float2(0.3f, 0.3f), tracks[0].HalfSize);
            Assert.AreEqual(1f, tracks[0].Value.x);
        }

        [Test]
        public void AddVehicle_FlattensItsWholeWidth()
        {
            var trample = new List<InteractionStamp>();

            InteractionMath.AddVehicle(Tank, float2.zero, new float2(1f, 0f), 0f, trample, new List<InteractionStamp>());

            Assert.AreEqual(1, trample.Count);
            Assert.AreEqual(1.7f, trample[0].HalfSize.x, 1e-5f);
            Assert.AreEqual(new float4(1f, 1f, 1f, 0f), trample[0].Value);
        }

        [Test]
        public void AddVehicle_ReachesBackOverTheDistanceDriven()
        {
            var tracks = new List<InteractionStamp>();

            InteractionMath.AddVehicle(Tank, new float2(0f, 10f), new float2(0f, 1f), 2f, new List<InteractionStamp>(), tracks);

            Assert.AreEqual(9f, tracks[0].Center.y, 1e-5f);
            Assert.AreEqual(1.3f, tracks[0].HalfSize.y, 1e-5f);
        }

        [Test]
        public void AddVehicle_ReachesForwardWhenReversing()
        {
            var tracks = new List<InteractionStamp>();

            InteractionMath.AddVehicle(Tank, new float2(0f, 10f), new float2(0f, 1f), -2f, new List<InteractionStamp>(), tracks);

            Assert.AreEqual(11f, tracks[0].Center.y, 1e-5f);
            Assert.AreEqual(1.3f, tracks[0].HalfSize.y, 1e-5f);
        }

        [Test]
        public void Footprint_FlattensForGoodWithoutSteering()
        {
            var stamp = InteractionMath.Footprint(new float2(5f, 6f), new float2(12f, 8f));

            Assert.AreEqual(new float2(6f, 4f), stamp.HalfSize);
            Assert.AreEqual(new float4(1f, 1f, 0f, 0f), stamp.Value);
        }

        private static void AssertNear(float2 expected, float2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-5f);
            Assert.AreEqual(expected.y, actual.y, 1e-5f);
        }

        [Test]
        public void Stride_MatchesTheStruct()
        {
            Assert.AreEqual(System.Runtime.InteropServices.Marshal.SizeOf<InteractionStamp>(), InteractionStamp.Stride);
        }
    }
}
