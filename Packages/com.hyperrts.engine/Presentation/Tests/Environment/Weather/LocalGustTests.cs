using HyperRTS.Presentation.Environment.Weather;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests.Environment.Weather
{
    /// <summary>The gusts rotors and blasts push into the grass and crops.</summary>
    public class LocalGustTests
    {
        private const float Reach = 25f;

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
