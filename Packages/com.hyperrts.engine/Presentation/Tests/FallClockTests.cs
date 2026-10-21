using HyperRTS.Presentation.Destruction;
using NUnit.Framework;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The felled tree's rod-on-a-stump fall: timing by height, the bounce and the rest.</summary>
    public class FallClockTests
    {
        private const float Frame = 1f / 60f;

        [TestCase(15f, 2f, 3f)]
        [TestCase(20f, 2.2f, 3.4f)]
        [TestCase(1.7f, 0.3f, 0.9f)]
        public void TallerTrees_TakeLongerToHitTheGround(float height, float min, float max)
        {
            var landed = LandingTime(FallClock.Pushed(height, 0f), height);

            Assert.That(landed, Is.InRange(min, max));
        }

        [Test]
        public void CrushingVehicle_PushesTheFallOn()
        {
            var shot = LandingTime(FallClock.Pushed(15f, 0f), 15f);
            var crushed = LandingTime(FallClock.Pushed(15f, 8f), 15f);

            Assert.Less(crushed, shot - 0.2f);
        }

        [Test]
        public void Landing_BouncesBackUp_ThenRestsLyingDown()
        {
            var clock = FallClock.Pushed(15f, 0f);
            LandingTime(ref clock, 15f);
            var bounced = false;
            for (var i = 0; i < 600 && !clock.Resting; i++)
            {
                var landing = clock.Advance(15f, Frame);
                bounced |= !landing && clock.Landed && clock.Progress < 0.995f;
            }

            Assert.IsTrue(bounced, "lifts off the canopy");
            Assert.IsTrue(clock.Resting, "settles within ten seconds");
            Assert.AreEqual(1f, clock.Progress, 0.01f);
        }

        [Test]
        public void SameFall_LooksTheSame_AtAnyFrameRate()
        {
            var slow = FallClock.Pushed(12f, 0f);
            var fast = FallClock.Pushed(12f, 0f);
            for (var i = 0; i < 60; i++)
            {
                slow.Advance(12f, 1f / 30f);
                fast.Advance(12f, 1f / 60f);
                fast.Advance(12f, 1f / 60f);
            }

            Assert.AreEqual(slow.Angle, fast.Angle, 0.01f);
        }

        private static float LandingTime(FallClock clock, float height) => LandingTime(ref clock, height);

        private static float LandingTime(ref FallClock clock, float height)
        {
            while (!clock.Advance(height, Frame))
            {
                Assert.Less(clock.Time, 10f, "never landed");
            }

            return clock.Time;
        }
    }
}
