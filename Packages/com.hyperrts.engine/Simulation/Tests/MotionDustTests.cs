using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Motion dust follows the drawn speed along the facing, not crowd pushes or creeping.</summary>
    public class MotionDustTests
    {
        private static readonly MotionDust Dust = new() { MinSpeed = 1.5f, FullSpeed = 8f };

        [Test]
        public void StandingOrCreeping_RaisesNoDust()
        {
            Assert.AreEqual(0f, Dust.Strength(0f));
            Assert.AreEqual(0f, Dust.Strength(Dust.MinSpeed));
        }

        [Test]
        public void Dust_GrowsWithSpeed_ToFullAtFullSpeed_EitherWay()
        {
            Assert.Greater(Dust.Strength(5f), Dust.Strength(3f));
            Assert.AreEqual(1f, Dust.Strength(8f), 1e-5f);
            Assert.AreEqual(1f, Dust.Strength(12f), 1e-5f);
            Assert.AreEqual(Dust.Strength(4f), Dust.Strength(-4f), 1e-5f, "reversing raises dust too");
        }

        [Test]
        public void DrawnSpeed_IsSignedAlongTheFacing_AndIgnoresSidewaysPushes()
        {
            var dust = Dust;
            var forward = new float3(0f, 0f, 1f);

            Assert.AreEqual(0f, dust.DrawnSpeed(float3.zero, forward, 0.1f), "the first frame has nothing to compare");
            Assert.AreEqual(6f, dust.DrawnSpeed(new float3(0f, 0f, 0.6f), forward, 0.1f), 1e-4f);
            Assert.AreEqual(-3f, dust.DrawnSpeed(new float3(0f, 0f, 0.3f), forward, 0.1f), 1e-4f, "reversing");
            Assert.AreEqual(0f, dust.DrawnSpeed(new float3(0.5f, 0f, 0.3f), forward, 0.1f), 1e-4f, "pushed sideways");
            Assert.AreEqual(0f, dust.DrawnSpeed(new float3(9f, 0f, 9f), forward, 0f), "paused");
        }
    }
}
