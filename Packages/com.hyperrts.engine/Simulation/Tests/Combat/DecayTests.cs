using System;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Remains that wait, are buried and go, and the ground they open when they go.</summary>
    public class DecayTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void DecayingWreck_IsOnlyReadWhileItWaits()
        {
            var wreck = SpawnWreck(2f, 1f, 3f);
            _world.Tick();
            var decayVersion = ChangeVersion<Decay>(wreck);
            var transformVersion = ChangeVersion<LocalTransform>(wreck);

            _world.Run(1f);

            Assert.AreEqual(decayVersion, ChangeVersion<Decay>(wreck), "a waiting wreck is only read");
            Assert.AreEqual(transformVersion, ChangeVersion<LocalTransform>(wreck));
            Assert.AreEqual(0f, _world.Get<Decay>(wreck).DespawnProgress);
            Assert.AreEqual(2f, _world.Get<LocalTransform>(wreck).Position.y, 1e-5f, "it stays put while it waits");
        }

        [Test]
        public void DecayingWreck_SinksByTheShareDone_ThenIsDestroyed()
        {
            var wreck = SpawnWreck(1f, 2f, 3f);

            _world.Run(2f);
            Assert.AreEqual(0.5f, _world.Get<Decay>(wreck).DespawnProgress, 0.05f);
            Assert.AreEqual(2f - 1.5f, _world.Get<LocalTransform>(wreck).Position.y, 0.15f);

            _world.Run(0.9f);
            Assert.IsTrue(_world.EntityManager.Exists(wreck), "still being buried");
            _world.Run(0.2f);
            Assert.IsFalse(_world.EntityManager.Exists(wreck));
        }

        [Test]
        public void DecaySetup_RejectsZeroDespawnDuration()
        {
            var wreck = _world.EntityManager.CreateEntity();
            var writer = new EntityManagerWriter(_world.EntityManager, wreck);

            var error = Assert.Throws<ArgumentException>(() => DecayAuthoring.Add(ref writer, "wreck", 180f, 0f, 3f));

            StringAssert.Contains("despawnDuration", error.Message);
        }

        // A ruin blocks its plot while it is buried and frees it for a new building once it has gone.
        [Test]
        public void DecayingRuin_OpensItsFootprintWhenItGoes()
        {
            var ruin = SpawnWreck(1f, 1f, 3f);
            var writer = new EntityManagerWriter(_world.EntityManager, ruin);
            NavSetup.AddObstacle(ref writer, new float2(6f, 6f));
            var plot = new float3(4f, 0f, 0f);

            _world.Tick();
            Assert.IsFalse(_world.Grid().IsWalkable(plot), "the ruin blocks its plot");

            _world.Run(1.5f);
            Assert.IsFalse(_world.Grid().IsWalkable(plot), "a ruin being buried still blocks");

            _world.Run(1f);
            Assert.IsFalse(_world.EntityManager.Exists(ruin));
            Assert.IsTrue(_world.Grid().IsWalkable(plot), "the plot opens once the ruin has gone");
        }

        private Entity SpawnWreck(float lifetime, float despawnDuration, float despawnDepth)
        {
            var wreck = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(wreck, LocalTransform.FromPosition(new float3(4f, 2f, 0f)));
            var writer = new EntityManagerWriter(_world.EntityManager, wreck);
            DecayAuthoring.Add(ref writer, "wreck", lifetime, despawnDuration, despawnDepth);
            return wreck;
        }

        private uint ChangeVersion<T>(Entity entity) where T : unmanaged, IComponentData
        {
            var handle = _world.EntityManager.GetComponentTypeHandle<T>(true);
            return _world.EntityManager.GetChunk(entity).GetChangeVersion(ref handle);
        }

    }
}
