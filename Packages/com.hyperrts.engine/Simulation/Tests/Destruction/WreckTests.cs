using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
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
    public class WreckTests : SimulationFixture
    {
        [Test]
        public void Wreck_StaysForTheMatch_WearsThroughItsStages_AndLeavesRuinsThatStay()
        {
            var ruin = World.MakePrefab(World.EntityManager.CreateEntity(typeof(LocalTransform)));
            var wreck = World.EntityManager.CreateEntity(typeof(LocalTransform));
            var writer = World.Writer(wreck);
            DestructibleAuthoring.AddScenery(writer, 300f, 0, ruin, float2.zero, default);
            var stages = World.EntityManager.AddBuffer<DamageStage>(wreck);
            stages.Add(new DamageStage { Below = 1f });
            stages.Add(new DamageStage { Below = 0.5f });

            World.Run(400f);
            Assert.IsTrue(World.EntityManager.Exists(wreck), "a wreck does not decay");
            Assert.AreEqual(0, Stage(wreck), "fresh while whole");

            World.QueueDamage(new DamageEvent { Position = float3.zero, Layers = WeaponTargets.Surface, Profile = new DamageProfile { Amount = 200f, Radius = 4f, EdgeFactor = 1f } });
            World.Tick();
            Assert.AreEqual(1, Stage(wreck), "burnt below half");

            World.QueueDamage(new DamageEvent { Position = float3.zero, Layers = WeaponTargets.Surface, Profile = new DamageProfile { Amount = 400f, Radius = 4f, EdgeFactor = 1f } });
            World.Tick();
            Assert.IsFalse(World.EntityManager.Exists(wreck));
            World.Run(400f);
            using var ruins = World.All<LocalTransform>();
            Assert.AreEqual(1, ruins.Length, "the blown-apart wreck is left");
        }

        private int Stage(Entity wreck)
        {
            var stages = World.Buffer<DamageStage>(wreck).AsNativeArray();
            return IModelStage.Pick(stages, World.Get<Health>(wreck).Fraction);
        }
    }
}
