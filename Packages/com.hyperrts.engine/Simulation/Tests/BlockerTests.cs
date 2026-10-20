using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Blockers (wrecks): units are pushed fully clear, the wreck never moves, a sinking one lets them by.</summary>
    public class BlockerTests
    {
        private const float WreckRadius = 1.85f;
        private const float WreckLength = 6.9f;
        private const float UnitRadius = 0.5f;
        private const float Reach = WreckRadius + UnitRadius;

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

        [Test]
        public void UnitInsideWreck_IsPushedFullyClear_AndTheWreckStays()
        {
            var wreck = SpawnWreck();
            var unit = _world.SpawnUnit(1, new float3(1f, 0f, 0.5f), radius: UnitRadius);

            _world.Tick();

            Assert.AreEqual(Reach, _world.GetPosition(unit).x, 0.01f, "out to the wreck's side in one frame");
            Assert.AreEqual(0.5f, _world.GetPosition(unit).z, 0.01f, "never pushed along the hull");
            Assert.AreEqual(float3.zero, _world.GetPosition(wreck), "the wreck never moves");
        }

        [Test]
        public void UnitDrivingIntoWreck_NeverEntersIt()
        {
            var wreck = SpawnWreck();
            var unit = DriveAcross(new float3(-10f, 0f, 1f));

            for (var frame = 0; frame < 150; frame++)
            {
                _world.Tick();
                Assert.LessOrEqual(_world.GetPosition(unit).x, -Reach + 0.01f, $"inside the wreck at frame {frame}");
            }

            Assert.AreEqual(float3.zero, _world.GetPosition(wreck), "the wreck never moves");
        }

        [Test]
        public void SinkingWreck_NoLongerBlocks()
        {
            var wreck = SpawnWreck();
            var writer = new EntityManagerWriter(_world.EntityManager, wreck);
            DecayAuthoring.Add(ref writer, "wreck", 2.5f, 30f, 3f);
            var unit = DriveAcross(new float3(-10f, 0f, 1f));

            _world.Run(2f);
            Assert.AreEqual(-Reach, _world.GetPosition(unit).x, 0.01f, "held at the wreck while it waits");

            _world.Run(4f);

            Assert.IsFalse(_world.IsEnabled<Blocker>(wreck), "it stops blocking once it sinks");
            Assert.AreEqual(10f, _world.GetPosition(unit).x, 0.5f, "the unit drove through");
        }
    }
}
