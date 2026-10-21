using HyperRTS.Simulation.Cues;
using NUnit.Framework;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Motion dust follows the driven speed, not crowd pushes or creeping.</summary>
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
    }
}
