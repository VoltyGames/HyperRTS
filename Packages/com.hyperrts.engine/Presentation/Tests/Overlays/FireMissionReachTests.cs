using HyperRTS.Presentation.Overlays;
using HyperRTS.Simulation.Combat.Weapons;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests.Overlays
{
    /// <summary>Which ground points a launcher's fire mission reaches from where it stands.</summary>
    public sealed class FireMissionReachTests
    {
        private static FireMissionReach Reach(float minRange, float range, float spread = 0f) =>
            new(new Weapon { MinRange = minRange, Range = range, Spread = spread });

        [TestCase(10f, true)]
        [TestCase(5f, false)]
        [TestCase(100f, true)]
        [TestCase(101f, false)]
        public void MeasuresFlatFromTheCentre(float distance, bool covered)
        {
            var reach = Reach(10f, 100f);
            var height = new float3(0f, 30f, 0f);

            Assert.AreEqual(covered, reach.Covers(height, new float3(distance, 0f, 0f)));
        }

        [Test]
        public void BlastAddsTheSpreadToTheSplash()
        {
            var weapon = new Weapon { Range = 50f, Spread = 6f };
            weapon.Damage.Radius = 4f;

            Assert.AreEqual(10f, new FireMissionReach(weapon).Blast);
        }
    }
}
