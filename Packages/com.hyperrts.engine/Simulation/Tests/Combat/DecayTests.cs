using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Navigation.Areas;
using System;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Remains that wait, are buried and go, and the ground they open when they go.</summary>
    public class DecayTests : SimulationFixture
    {
        [Test]
        public void DecayingWreck_IsOnlyReadWhileItWaits()
        {
            var wreck = SpawnWreck(2f, 1f, 3f);
            World.Tick();
            var decayVersion = ChangeVersion<Decay>(wreck);
            var transformVersion = ChangeVersion<LocalTransform>(wreck);

            World.Run(1f);

            Assert.AreEqual(decayVersion, ChangeVersion<Decay>(wreck), "a waiting wreck is only read");
            Assert.AreEqual(transformVersion, ChangeVersion<LocalTransform>(wreck));
            Assert.AreEqual(0f, World.Get<Decay>(wreck).DespawnProgress);
            Assert.AreEqual(2f, World.Get<LocalTransform>(wreck).Position.y, 1e-5f, "it stays put while it waits");
        }

        [Test]
        public void DecayingWreck_SinksByTheShareDone_ThenIsDestroyed()
        {
            var wreck = SpawnWreck(1f, 2f, 3f);

            World.Run(2f);
            Assert.AreEqual(0.5f, World.Get<Decay>(wreck).DespawnProgress, 0.05f);
            Assert.AreEqual(2f - 1.5f, World.Get<LocalTransform>(wreck).Position.y, 0.15f);

            World.Run(0.9f);
            Assert.IsTrue(World.EntityManager.Exists(wreck), "still being buried");
            World.Run(0.2f);
            Assert.IsFalse(World.EntityManager.Exists(wreck));
        }

        // A ruin blocks its plot while it is buried and frees it for a new building once it has gone.
        [Test]
        public void DecayingRuin_OpensItsFootprintWhenItGoes()
        {
            var ruin = SpawnWreck(1f, 1f, 3f);
            var writer = World.Writer(ruin);
            NavSetup.AddObstacle(writer, new float2(6f, 6f));
            var plot = new float3(4f, 0f, 0f);

            World.Tick();
            Assert.IsFalse(World.Grid().IsWalkable(plot), "the ruin blocks its plot");

            World.Run(1.5f);
            Assert.IsFalse(World.Grid().IsWalkable(plot), "a ruin being buried still blocks");

            World.Run(1f);
            Assert.IsFalse(World.EntityManager.Exists(ruin));
            Assert.IsTrue(World.Grid().IsWalkable(plot), "the plot opens once the ruin has gone");
        }

        private Entity SpawnWreck(float lifetime, float despawnDuration, float despawnDepth)
        {
            var wreck = World.EntityManager.CreateEntity();
            World.Add(wreck, LocalTransform.FromPosition(new float3(4f, 2f, 0f)));
            var writer = World.Writer(wreck);
            DecayAuthoring.Add(writer, "wreck", lifetime, despawnDuration, despawnDepth);
            return wreck;
        }

        private uint ChangeVersion<T>(Entity entity) where T : unmanaged, IComponentData
        {
            var handle = World.EntityManager.GetComponentTypeHandle<T>(true);
            return World.EntityManager.GetChunk(entity).GetChangeVersion(ref handle);
        }

    }
}
