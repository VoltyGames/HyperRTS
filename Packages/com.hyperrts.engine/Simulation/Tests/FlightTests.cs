using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The Air nav layer: straight flight at altitude, hovering, loitering and air-only separation.</summary>
    public class FlightTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.ConfigureMap(waterLevel: -1f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Aircraft_FlyStraightOverWaterAndObstacles_AtAltitude()
        {
            _world.SpawnNavArea(float3.zero, new float2(10f, 40f), NavAreaKind.Water);
            _world.SpawnNavArea(new float3(10f, 0f, 0f), new float2(4f, 40f), NavAreaKind.Blocked);
            var aircraft = _world.SpawnAircraft(1, new float3(-15f, 0f, 0f), altitude: 10f);
            _world.MoveTo(aircraft, new float3(20f, 0f, 0f));

            for (var t = 0f; t < 6f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                Assert.AreEqual(0f, _world.PositionOf(aircraft).z, 1e-3f, "flew a straight line");
            }

            var position = _world.PositionOf(aircraft);
            Assert.Less(math.distance(position.xz, new float2(20f, 0f)), 0.5f);
            Assert.AreEqual(10f, position.y, 1e-3f, "cruises at its altitude over the ground");
        }

        [Test]
        public void IdleHelicopter_Hovers_WhileIdleJet_KeepsCircling()
        {
            var helicopter = _world.SpawnAircraft(1, new float3(-10f, 0f, 0f));
            var jet = _world.SpawnAircraft(1, new float3(10f, 0f, 0f), loiterRadius: 5f);
            _world.Run(2f);

            var hover = _world.PositionOf(helicopter);
            var before = _world.PositionOf(jet);
            _world.Run(1f);

            Assert.AreEqual(hover, _world.PositionOf(helicopter), "a helicopter holds still");
            var after = _world.PositionOf(jet);
            Assert.Greater(math.distance(before.xz, after.xz), 1f, "a jet keeps flying");
            Assert.Less(math.distance(after.xz, new float2(10f, 0f)), 10.5f, "within its loiter circle");
        }

        [Test]
        public void Aircraft_OnlySeparateFromAircraft()
        {
            var tank = _world.SpawnUnit(1, float3.zero);
            var helicopter = _world.SpawnAircraft(1, float3.zero);
            var other = _world.SpawnAircraft(1, new float3(0.2f, 0f, 0f));
            _world.Run(1f);

            Assert.AreEqual(0f, math.length(_world.PositionOf(tank).xz), 1e-4f, "the tank under them isn't pushed");
            var gap = math.distance(_world.PositionOf(helicopter).xz, _world.PositionOf(other).xz);
            Assert.GreaterOrEqual(gap, 0.99f, "aircraft spread apart");
        }
    }
}
