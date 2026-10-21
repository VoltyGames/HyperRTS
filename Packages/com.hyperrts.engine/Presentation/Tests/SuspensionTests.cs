using HyperRTS.Presentation.Animation;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>A ground vehicle's body sway: squat, dive, roll and recoil, and the spring settling.</summary>
    public class SuspensionTests
    {
        private const float Frame = 1f / 60f;

        [Test]
        public void SpeedingUp_SquatsNoseUp_AndBraking_DivesNoseDown()
        {
            Assert.Greater(Springs().Target(math.forward() * 2f, math.forward(), math.right()).x, 0f);
            Assert.Less(Springs().Target(math.back() * 2f, math.forward(), math.right()).x, 0f);
        }

        [Test]
        public void RightTurn_RollsTheRightSideUp()
        {
            var target = Springs().Target(math.right() * 3f, math.forward(), math.right());

            Assert.Greater(target.y, 0f);
            Assert.AreEqual(0f, target.x, 1e-6f);
        }

        [Test]
        public void HardBraking_SwaysNoMoreThanTheLimit()
        {
            var springs = Springs();

            var target = springs.Target(math.back() * 40f, math.forward(), math.right());

            Assert.AreEqual(-springs.MaxSway, target.x, 1e-6f);
        }

        [Test]
        public void Spring_OvershootsThenSettlesOnTheTarget()
        {
            var springs = Springs();
            var target = new float2(0.02f, -0.01f);
            var peak = 0f;
            for (var i = 0; i < 300; i++)
            {
                springs.Spring(target, Frame);
                peak = math.max(peak, springs.Sway.x);
            }

            Assert.Greater(peak, target.x, "bounces past on underdamped springs");
            MathAssert.Near(new float3(target, 0f), new float3(springs.Sway, 0f), 1e-4f);
            Assert.Less(math.length(springs.Rate), 1e-3f);
        }

        [Test]
        public void ShotForward_RocksTheNoseUp_ThenComesBackLevel()
        {
            var springs = Springs();
            springs.Recoil(0f);
            springs.Spring(float2.zero, Frame * 6f);
            Assert.Greater(springs.Sway.x, 0f);
            Assert.AreEqual(0f, springs.Sway.y, 1e-6f);

            for (var i = 0; i < 300; i++)
            {
                springs.Spring(float2.zero, Frame);
            }

            Assert.Less(math.length(springs.Sway), 1e-4f);
        }

        [Test]
        public void LongHitch_StaysStable()
        {
            var springs = Springs();

            springs.Spring(new float2(0.03f, 0f), 0.5f);

            Assert.LessOrEqual(math.abs(springs.Sway.x), springs.MaxSway);
            Assert.IsFalse(math.any(math.isnan(springs.Rate)));
        }

        [Test]
        public void ZeroSprings_NeverSwayOrKick()
        {
            var aircraft = default(Suspension);
            aircraft.Recoil(0f);
            aircraft.Spring(aircraft.Target(math.forward() * 5f, math.forward(), math.right()), Frame);

            Assert.AreEqual(float2.zero, aircraft.Sway);
            Assert.AreEqual(float2.zero, aircraft.Rate);
        }

        private static Suspension Springs() => new()
        {
            Frequency = 1.5f,
            Damping = 0.45f,
            PerAcceleration = math.radians(0.6f),
            MaxSway = math.radians(3f),
            Kick = math.radians(12f),
        };
    }
}
