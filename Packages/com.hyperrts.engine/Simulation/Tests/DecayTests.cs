using System;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Remains that wait out their lifetime, sink and are destroyed.</summary>
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

        private Entity SpawnWreck(float lifetime, float despawnDuration, float despawnDepth)
        {
            var wreck = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(wreck, LocalTransform.FromPosition(new float3(4f, 2f, 0f)));
            var writer = new EntityManagerWriter(_world.EntityManager, wreck);
            DecayAuthoring.Add(ref writer, "wreck", lifetime, despawnDuration, despawnDepth);
            return wreck;
        }

        [Test]
        public void Waiting_LeavesTheRemainsUnwritten()
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

        private uint ChangeVersion<T>(Entity entity) where T : unmanaged, IComponentData
        {
            var handle = _world.EntityManager.GetComponentTypeHandle<T>(true);
            return _world.EntityManager.GetChunk(entity).GetChangeVersion(ref handle);
        }

        [Test]
        public void Despawn_LowersTheRemains_ByTheShareDone()
        {
            var wreck = SpawnWreck(1f, 2f, 3f);

            _world.Run(2f);

            var decay = _world.Get<Decay>(wreck);
            Assert.AreEqual(0.5f, decay.DespawnProgress, 0.05f);
            Assert.AreEqual(2f - 1.5f, _world.Get<LocalTransform>(wreck).Position.y, 0.15f);
        }

        [Test]
        public void Remains_AreDestroyed_OnceDespawned()
        {
            var wreck = SpawnWreck(1f, 2f, 3f);

            _world.Run(2.9f);
            Assert.IsTrue(_world.EntityManager.Exists(wreck), "still sinking");

            _world.Run(0.2f);
            Assert.IsFalse(_world.EntityManager.Exists(wreck));
        }

        [Test]
        public void Setup_RejectsZeroDespawnDuration()
        {
            var wreck = _world.EntityManager.CreateEntity();
            var writer = new EntityManagerWriter(_world.EntityManager, wreck);

            var error = Assert.Throws<ArgumentException>(() => DecayAuthoring.Add(ref writer, "wreck", 180f, 0f, 3f));

            StringAssert.Contains("despawnDuration", error.Message);
        }
    }
}
