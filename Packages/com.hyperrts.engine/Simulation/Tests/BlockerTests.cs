using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Blockers (wrecks) close their land cells: paths go round, units never enter, a sinking one opens them.</summary>
    public class BlockerTests
    {
        private const float WreckRadius = 1.85f;
        private const float WreckLength = 6.9f;
        private const float UnitRadius = 0.5f;

        // A unit's clearance from closed cells is a little under its radius.
        private const float Reach = WreckRadius + UnitRadius - 0.1f;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        // Lies crosswise along Z at the origin, so a unit driving along X meets its long side.
        private Entity SpawnWreck()
        {
            var wreck = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(wreck, LocalTransform.FromPosition(float3.zero));
            var writer = new EntityManagerWriter(_world.EntityManager, wreck);
            BlockerAuthoring.Add(ref writer, "wreck", WreckRadius, WreckLength);
            return wreck;
        }

        private Entity DriveAcross(float3 start)
        {
            var unit = _world.SpawnUnit(1, start, radius: UnitRadius);
            _world.EntityManager.SetComponentData(unit, new MoveDestination { Value = new float3(10f, 0f, start.z) });
            _world.EntityManager.SetComponentEnabled<MoveDestination>(unit, true);
            return unit;
        }

        private float Gap(Entity unit)
        {
            var half = WreckLength * 0.5f - WreckRadius;
            var position = _world.GetPosition(unit);
            return math.distance(position.xz, new float2(0f, math.clamp(position.z, -half, half)));
        }

        [Test]
        public void Wreck_ClosesTheLandItCovers()
        {
            SpawnWreck();
            _world.Tick();

            var grid = _world.Grid();
            Assert.IsFalse(grid.IsWalkable(new float3(0f, 0f, 3f)), "under the hull");
            Assert.IsFalse(grid.IsWalkable(new float3(-1.5f, 0f, 0f)), "under its side");
            Assert.IsTrue(grid.IsWalkable(new float3(-2.5f, 0f, 0f)), "beside it");
        }

        [Test]
        public void UnitDrivingAtWreck_GoesRound_NeverEnteringIt()
        {
            var wreck = SpawnWreck();
            var unit = DriveAcross(new float3(-10f, 0f, 1f));

            for (var frame = 0; frame < 300; frame++)
            {
                _world.Tick();
                Assert.GreaterOrEqual(Gap(unit), Reach, $"inside the wreck at frame {frame}");
            }

            Assert.AreEqual(10f, _world.GetPosition(unit).x, 0.5f, "it went round to the goal");
            Assert.AreEqual(float3.zero, _world.GetPosition(wreck), "the wreck never moves");
        }

        [Test]
        public void UnitCaughtByANewWreck_DrivesOut()
        {
            SpawnWreck();
            var unit = DriveAcross(new float3(1f, 0f, 0.5f));

            _world.Run(6f);

            Assert.AreEqual(10f, _world.GetPosition(unit).x, 0.5f, "it backed out of the closed cells and went on");
        }

        [Test]
        public void SinkingWreck_OpensItsCells()
        {
            var wreck = SpawnWreck();
            var writer = new EntityManagerWriter(_world.EntityManager, wreck);
            DecayAuthoring.Add(ref writer, "wreck", 2.5f, 30f, 3f);
            _world.Tick();
            Assert.IsFalse(_world.Grid().IsWalkable(float3.zero), "it blocks while it waits");

            _world.Run(3f);
            Assert.IsFalse(_world.IsEnabled<Blocker>(wreck), "it stops blocking once it sinks");
            Assert.IsTrue(_world.Grid().IsWalkable(float3.zero), "its cells open again");

            var unit = DriveAcross(new float3(-10f, 0f, 0f));
            for (var frame = 0; frame < 150; frame++)
            {
                _world.Tick();
                Assert.AreEqual(0f, _world.GetPosition(unit).z, 0.2f, $"left the straight line at frame {frame}");
            }

            Assert.AreEqual(10f, _world.GetPosition(unit).x, 0.5f, "the unit drove straight through");
        }
    }
}
