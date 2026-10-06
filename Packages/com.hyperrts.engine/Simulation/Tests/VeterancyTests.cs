using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Veterancy;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Stat modifiers, kill credit and veterancy ranks.</summary>
    public class VeterancyTests
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

        private Entity Veteran(float3 position)
        {
            var unit = _world.Arm(_world.SpawnUnit(1, position), damage: 50f);
            var writer = new EntityManagerWriter(_world.EntityManager, unit);
            VeterancySetup.Add(ref writer, new[] { 10f, 100f }, new[]
            {
                new VeterancyBonus
                {
                    Rank = 1,
                    Modifier = new StatModifier { Stat = Stat.Damage, Percent = 0.5f, Source = StatSource.Veterancy },
                },
            });
            return unit;
        }

        [Test]
        public void Modifiers_RewriteLiveStats_AndKeepHealthFraction()
        {
            var unit = _world.SpawnUnit(1, float3.zero, speed: 4f);
            _world.EntityManager.SetComponentData(unit, new Health { Current = 50f, Max = 100f });
            var modifiers = _world.EntityManager.GetBuffer<StatModifier>(unit);
            modifiers.Add(new StatModifier { Stat = Stat.MaxHealth, Add = 100f, Source = StatSource.Upgrade(7) });
            modifiers.Add(new StatModifier { Stat = Stat.MoveSpeed, Percent = 0.5f, Source = StatSource.Upgrade(7) });

            _world.Tick();

            Assert.AreEqual(200f, _world.Get<Health>(unit).Max, 1e-3f);
            Assert.AreEqual(100f, _world.Get<Health>(unit).Current, 1e-3f);
            Assert.AreEqual(6f, _world.Get<MovementSpeed>(unit).Value, 1e-3f);

            StatMath.RemoveSource(_world.EntityManager.GetBuffer<StatModifier>(unit), StatSource.Upgrade(7));
            _world.Tick();

            Assert.AreEqual(100f, _world.Get<Health>(unit).Max, 1e-3f);
            Assert.AreEqual(4f, _world.Get<MovementSpeed>(unit).Value, 1e-3f, "removing the source restores the base");
        }

        [Test]
        public void Kill_EarnsExperience_AndRankBonus()
        {
            var veteran = Veteran(float3.zero);
            var enemy = _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            _world.Run(3f);

            Assert.IsFalse(_world.EntityManager.Exists(enemy));
            var experience = _world.Get<Experience>(veteran);
            Assert.AreEqual(10f, experience.Points, 1e-3f, "the default experience value");
            Assert.AreEqual(1, experience.Rank);
            Assert.AreEqual(75f, _world.Get<Weapon>(veteran).Damage, 1e-3f, "+50% damage at rank 1");
        }

        [Test]
        public void AlliedKills_EarnNothing()
        {
            var veteran = Veteran(float3.zero);
            var friend = _world.SpawnUnit(1, new float3(30f, 0f, 0f));
            _world.EntityManager.SetComponentData(friend, new LastAttacker { Source = veteran, Faction = 1 });
            _world.EntityManager.SetComponentData(friend, new Health { Current = 0f, Max = 100f });

            _world.Tick();

            Assert.AreEqual(0f, _world.Get<Experience>(veteran).Points);
        }
    }
}
