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

        private Entity SpawnWreck(float lifetime, float sinkDuration, float sinkDepth)
        {
            var wreck = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(wreck, LocalTransform.FromPosition(new float3(4f, 2f, 0f)));
            var writer = new EntityManagerWriter(_world.EntityManager, wreck);
            DecaySetup.Add(ref writer, "wreck", lifetime, sinkDuration, sinkDepth);
            return wreck;
        }

        [Test]
        public void Lifetime_CountsDown_WithoutSinking()
        {
            var wreck = SpawnWreck(2f, 1f, 3f);

            _world.Run(1f);

            var decay = _world.Get<Decay>(wreck);
            Assert.AreEqual(1f, decay.Lifetime, 0.05f);
            Assert.AreEqual(0f, decay.Sunk);
            Assert.AreEqual(2f, _world.Get<LocalTransform>(wreck).Position.y, 1e-5f, "it stays put while it waits");
        }

        [Test]
        public void Sink_LowersTheRemains_ByTheShareDone()
        {
            var wreck = SpawnWreck(1f, 2f, 3f);

            _world.Run(2f);

            var decay = _world.Get<Decay>(wreck);
            Assert.AreEqual(0.5f, decay.Sunk, 0.05f);
            Assert.AreEqual(2f - 1.5f, _world.Get<LocalTransform>(wreck).Position.y, 0.15f);
        }

        [Test]
        public void Remains_AreDestroyed_OnceSunk()
        {
            var wreck = SpawnWreck(1f, 2f, 3f);

            _world.Run(2.9f);
            Assert.IsTrue(_world.EntityManager.Exists(wreck), "still sinking");

            _world.Run(0.2f);
            Assert.IsFalse(_world.EntityManager.Exists(wreck));
        }

        [Test]
        public void Setup_RejectsZeroSinkDuration()
        {
            var wreck = _world.EntityManager.CreateEntity();
            var writer = new EntityManagerWriter(_world.EntityManager, wreck);

            var error = Assert.Throws<ArgumentException>(() => DecaySetup.Add(ref writer, "wreck", 180f, 0f, 3f));

            StringAssert.Contains("sinkDuration", error.Message);
        }
    }
}
