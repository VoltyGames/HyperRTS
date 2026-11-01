using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Vision
{
    /// <summary>Tall targets seen over low cover, and high ground seeing further within its cap.</summary>
    public class HighGroundTests
    {
        private const byte Team1 = 1;
        private const byte Team2 = 2;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(Team1, Team2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void TankBehindAWall_IsSeen_InfantryAreNot()
        {
            _world.SpawnViewer(Team1, new float3(1f, 0f, 1f), 30f);
            _world.SpawnSightBlock(new float3(7f, 0f, 1f), new float2(0.4f, 10f), 1.8f);
            var infantry = _world.SpawnUnit(Team2, new float3(15f, 0f, 1f));
            var tank = _world.SpawnUnit(Team2, new float3(15f, 0f, 3f));
            _world.EntityManager.AddComponent<TallTarget>(tank);

            _world.Tick(frames: 2);

            Assert.IsTrue(_world.IsFogHidden(infantry), "infantry hide behind the wall");
            Assert.IsFalse(_world.IsFogHidden(tank), "a tank shows over it");
        }

        // A cone 20 m across the base on flat ground, the viewer on its top with 20 m of sight. Its rise over the mean
        // within 50 m is a little under its height: about +19 % sight for 10 m, and past the +25 % cap for 30 m.
        [TestCase(10f, 23f, 25f, TestName = "Kurgan_SeesFurther")]
        [TestCase(30f, 25f, 27f, TestName = "TallKurgan_SeesNoFurtherThanTheCap")]
        public void ViewerOnAKurgan_SeesFurther_WithinTheCap(float height, float seen, float unseen)
        {
            _world.CreateTerrain(p => math.max(0f, height * (1f - math.distance(p, new float2(1f, 1f)) / 20f)));
            _world.SpawnViewer(Team1, new float3(1f, height, 1f), 20f);

            _world.Tick();

            var fog = _world.Fog();
            Assert.IsTrue(fog.IsVisible(new float3(1f + seen - 1f, 0f, 1f), Team1, false), "within the bonus");
            Assert.IsFalse(fog.IsVisible(new float3(1f + unseen + 1f, 0f, 1f), Team1, false), "beyond it");
        }
    }
}
