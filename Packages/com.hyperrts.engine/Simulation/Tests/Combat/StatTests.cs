using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Veterancy;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Stat modifiers on live stats and weapons, kill credit and veterancy ranks.</summary>
    public class StatTests
    {
        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = _world.CreateSupplies();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

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
        public void Modifiers_UseTheSpawnBase_WhenTheLiveStatChangedFirst()
        {
            var unit = _world.SpawnUnit(1, float3.zero, speed: 4f);
            _world.Tick();
            _world.EntityManager.SetComponentData(unit, new MovementSpeed { Value = 9f });
            _world.EntityManager.GetBuffer<StatModifier>(unit)
                .Add(new StatModifier { Stat = Stat.MoveSpeed, Percent = 0.5f, Source = StatSource.Upgrade(7) });

            _world.Tick();

            Assert.AreEqual(6f, _world.Get<MovementSpeed>(unit).Value, 1e-3f);
        }

        [TestCase(0f, 1f, 0.25f, TestName = "FireRate +100% halves the cooldown")]
        [TestCase(0f, -0.5f, 1f, TestName = "FireRate -50% doubles the cooldown")]
        [TestCase(1f, 0f, 1f / 3f, TestName = "FireRate +1 shot per second turns 2/s into 3/s")]
        public void FireRateModifier_ScalesCooldown(float add, float percent, float expected)
        {
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), Stance.Passive, cooldown: 0.5f);
            _world.EntityManager.GetBuffer<StatModifier>(soldier).Add(new StatModifier
            {
                Stat = Stat.FireRate, Add = add, Percent = percent,
            });

            _world.Tick();

            Assert.AreEqual(expected, _world.Get<Weapon>(soldier).Cooldown, 1e-5f);
        }

        [Test]
        public void DamageModifier_ReachesMounts()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), Stance.Passive);
            _world.AddMount(gun, range: 5f, damage: 40f, cooldown: 2f);
            _world.EntityManager.GetBuffer<StatModifier>(gun).Add(new StatModifier
            {
                Stat = Stat.Damage, Percent = 0.25f, Source = StatSource.Upgrade(7),
            });

            _world.Tick();

            var mount = _world.Mount(gun);
            Assert.AreEqual(50f, mount.Weapon.Damage, 1e-4f);
            Assert.AreEqual(5f, mount.Weapon.Range, 1e-4f);
            Assert.AreEqual(2f, mount.Weapon.Cooldown, 1e-4f);
        }

        [Test]
        public void Kill_EarnsExperience_AndRankBonus()
        {
            var veteran = Veteran(float3.zero);
            var enemy = Victim(2, new float3(3f, 0f, 0f), cost: 100);

            _world.Run(3f);

            Assert.IsFalse(_world.EntityManager.Exists(enemy));
            var experience = _world.Get<Experience>(veteran);
            Assert.AreEqual(10f, experience.Points, 1e-3f, "cost 100 times the match's 0.1");
            Assert.AreEqual(1, experience.Rank);
            Assert.AreEqual(75f, _world.Get<Weapon>(veteran).Damage, 1e-3f, "+50% damage at rank 1");
        }

        [Test]
        public void KillCredit_PaysTheEnemysCostTimesTheMatchFactor_AndNothingForAllies()
        {
            var veteran = Veteran(float3.zero);
            using var query = _world.EntityManager.CreateEntityQuery(typeof(MatchRules));
            var rules = query.GetSingleton<MatchRules>();
            rules.ExperiencePerCost = 0.5f;
            query.SetSingleton(rules);
            var enemy = Victim(2, new float3(30f, 0f, 0f), cost: 40);
            var friend = Victim(1, new float3(-30f, 0f, 0f), cost: 100);
            foreach (var victim in new[] { enemy, friend })
            {
                _world.EntityManager.SetComponentData(victim, new LastAttacker { Source = veteran, Faction = 1 });
                _world.Kill(victim);
            }

            _world.Tick();

            Assert.AreEqual(20f, _world.Get<Experience>(veteran).Points, 1e-3f, "only the enemy pays");
        }

        private Entity Veteran(float3 position)
        {
            var unit = _world.AddWeapon(_world.SpawnUnit(1, position), damage: 50f);
            var writer = new EntityManagerWriter(_world.EntityManager, unit);
            VeterancyAuthoring.Add(ref writer, new[] { 10f, 100f }, new[]
            {
                new VeterancyBonus
                {
                    Rank = 1,
                    Modifier = new StatModifier { Stat = Stat.Damage, Percent = 0.5f, Source = StatSource.Veterancy },
                },
            });
            return unit;
        }

        private Entity Victim(byte faction, float3 position, int cost)
        {
            var victim = _world.SpawnUnit(faction, position);
            _world.SetCost(victim, _supplies, cost);
            return victim;
        }
    }
}
