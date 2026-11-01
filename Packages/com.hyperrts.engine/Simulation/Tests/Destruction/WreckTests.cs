using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Destruction;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Destruction
{
    /// <summary>A wreck without Decay stays all match, wears through its damage stages and leaves ruins that stay too.</summary>
    public class WreckTests
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
        public void Wreck_StaysForTheMatch_WearsThroughItsStages_AndLeavesRuinsThatStay()
        {
            var ruin = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            var wreck = _world.EntityManager.CreateEntity(typeof(LocalTransform));
            var writer = new EntityWriter(_world.EntityManager, wreck);
            DestructibleAuthoring.AddScenery(writer, 300f, 0, ruin, float2.zero, default);
            var stages = _world.EntityManager.AddBuffer<DamageStage>(wreck);
            stages.Add(new DamageStage { Below = 1f });
            stages.Add(new DamageStage { Below = 0.5f });

            _world.Run(400f);
            Assert.IsTrue(_world.EntityManager.Exists(wreck), "a wreck does not decay");
            Assert.AreEqual(0, Stage(wreck), "fresh while whole");

            _world.QueueDamage(new DamageEvent { Position = float3.zero, Amount = 200f, Radius = 4f, EdgeFactor = 1f });
            _world.Tick();
            Assert.AreEqual(1, Stage(wreck), "burnt below half");

            _world.QueueDamage(new DamageEvent { Position = float3.zero, Amount = 400f, Radius = 4f, EdgeFactor = 1f });
            _world.Tick();
            Assert.IsFalse(_world.EntityManager.Exists(wreck));
            _world.Run(400f);
            using var ruins = _world.All<LocalTransform>();
            Assert.AreEqual(1, ruins.Length, "the blown-apart wreck is left");
        }

        private int Stage(Entity wreck)
        {
            var stages = _world.EntityManager.GetBuffer<DamageStage>(wreck).AsNativeArray();
            return IModelStage.Pick(stages, _world.Get<Health>(wreck).Fraction);
        }
    }
}
