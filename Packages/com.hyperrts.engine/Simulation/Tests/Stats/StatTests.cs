using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Veterancy;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Stats
{
    /// <summary>Stat modifiers on live stats and weapons, kill credit and veterancy ranks.</summary>
    public class StatTests : SimulationFixture
    {
        private ResourceType _supplies;

        /// <summary>A game's own component; its stat field sits after another field.</summary>
        public struct Jamming : IComponentData
        {
            public int Channels;
            [StatField(Stat.GameFirst)] public float Strength;
        }

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
        }

        [Test]
        public void Modifiers_RewriteLiveStats_AndKeepHealthFraction()
        {
            var unit = World.SpawnUnit(1, float3.zero, new TestUnit { Speed = 4f });
            World.Set(unit, new Health { Current = 50f, Max = 100f });
            var modifiers = World.Buffer<StatModifier>(unit);
            modifiers.Add(new StatModifier { Stat = Stat.MaxHealth, Add = 100f, Source = StatSource.Upgrade(7) });
            modifiers.Add(new StatModifier { Stat = Stat.MoveSpeed, Percent = 0.5f, Source = StatSource.Upgrade(7) });

            World.Tick();

            Assert.AreEqual(200f, World.Get<Health>(unit).Max, 1e-3f);
            Assert.AreEqual(100f, World.Get<Health>(unit).Current, 1e-3f);
            Assert.AreEqual(6f, World.Get<MovementSpeed>(unit).Value, 1e-3f);

            new StatModifiers(World.Buffer<StatModifier>(unit)).RemoveSource(StatSource.Upgrade(7));
            World.Tick();

            Assert.AreEqual(100f, World.Get<Health>(unit).Max, 1e-3f);
            Assert.AreEqual(4f, World.Get<MovementSpeed>(unit).Value, 1e-3f, "removing the source restores the base");
        }

        [Test]
        public void Modifiers_UseTheSpawnBase_WhenTheLiveStatChangedFirst()
        {
            var unit = World.SpawnUnit(1, float3.zero, new TestUnit { Speed = 4f });
            World.Tick();
            World.Set(unit, new MovementSpeed { Value = 9f });
            World.Buffer<StatModifier>(unit)
                .Add(new StatModifier { Stat = Stat.MoveSpeed, Percent = 0.5f, Source = StatSource.Upgrade(7) });

            World.Tick();

            Assert.AreEqual(6f, World.Get<MovementSpeed>(unit).Value, 1e-3f);
        }

        [Test]
        public void StatField_DrivesAGameDefinedStat()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            World.EntityManager.AddComponentData(unit, new Jamming { Channels = 3, Strength = 10f });
            World.Buffer<StatModifier>(unit).Add(new StatModifier
            {
                Stat = Stat.GameFirst, Percent = 0.5f, Source = StatSource.Upgrade(7),
            });

            World.Tick();

            Assert.AreEqual(15f, World.Get<Jamming>(unit).Strength, 1e-4f);
            Assert.AreEqual(3, World.Get<Jamming>(unit).Channels, "other fields are untouched");

            new StatModifiers(World.Buffer<StatModifier>(unit)).RemoveSource(StatSource.Upgrade(7));
            World.Tick();

            Assert.AreEqual(10f, World.Get<Jamming>(unit).Strength, 1e-4f);
        }

        [TestCase(0f, 1f, 0.25f, TestName = "FireRate +100% halves the cooldown")]
        [TestCase(0f, -0.5f, 1f, TestName = "FireRate -50% doubles the cooldown")]
        [TestCase(1f, 0f, 1f / 3f, TestName = "FireRate +1 shot per second turns 2/s into 3/s")]
        public void FireRateModifier_ScalesCooldown(float add, float percent, float expected)
        {
            var soldier = World.AddWeapon(World.SpawnUnit(1, float3.zero), new TestWeapon
            {
                Stance = Stance.Passive,
                Cooldown = 0.5f,
            });
            World.Buffer<StatModifier>(soldier).Add(new StatModifier
            {
                Stat = Stat.FireRate, Add = add, Percent = percent,
            });

            World.Tick();

            Assert.AreEqual(expected, World.Primary(soldier).Cooldown, 1e-5f);
        }

        [Test]
        public void DamageModifier_ReachesMounts()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), new TestWeapon { Stance = Stance.Passive });
            World.AddMount(gun, new TestMount { Range = 5f, Damage = 40f, Cooldown = 2f });
            World.Buffer<StatModifier>(gun).Add(new StatModifier
            {
                Stat = Stat.Damage, Percent = 0.25f, Source = StatSource.Upgrade(7),
            });

            World.Tick();

            var mount = World.Mount(gun);
            Assert.AreEqual(50f, mount.Weapon.Damage.Amount, 1e-4f);
            Assert.AreEqual(5f, mount.Weapon.Range, 1e-4f);
            Assert.AreEqual(2f, mount.Weapon.Cooldown, 1e-4f);
        }

        [Test]
        public void Kill_EarnsExperience_AndRankBonus()
        {
            var veteran = Veteran(float3.zero);
            var enemy = Victim(2, new float3(3f, 0f, 0f), cost: 100);

            World.Run(3f);

            Assert.IsFalse(World.EntityManager.Exists(enemy));
            var experience = World.Get<Experience>(veteran);
            Assert.AreEqual(10f, experience.Points, 1e-3f, "cost 100 times the match's 0.1");
            Assert.AreEqual(1, experience.Rank);
            Assert.AreEqual(75f, World.Primary(veteran).Damage.Amount, 1e-3f, "+50% damage at rank 1");
        }

        [Test]
        public void KillCredit_PaysTheEnemysCostTimesTheMatchFactor_AndNothingForAllies()
        {
            var veteran = Veteran(float3.zero);
            using var query = World.Query(typeof(MatchRules));
            var rules = query.GetSingleton<MatchRules>();
            rules.ExperiencePerCost = 0.5f;
            query.SetSingleton(rules);
            var enemy = Victim(2, new float3(30f, 0f, 0f), cost: 40);
            var friend = Victim(1, new float3(-30f, 0f, 0f), cost: 100);
            foreach (var victim in new[] { enemy, friend })
            {
                World.Set(victim, new LastAttacker { Source = veteran, Owner = 1 });
                World.Kill(victim);
            }

            World.Tick();

            Assert.AreEqual(20f, World.Get<Experience>(veteran).Points, 1e-3f, "only the enemy pays");
        }

        private Entity Veteran(float3 position)
        {
            var unit = World.AddWeapon(World.SpawnUnit(1, position), new TestWeapon { Damage = 50f });
            var writer = World.Writer(unit);
            VeterancyAuthoring.Add(writer, new[] { 10f, 100f }, new[]
            {
                new VeterancyBonus
                {
                    Rank = 1,
                    Modifier = new StatModifier { Stat = Stat.Damage, Percent = 0.5f, Source = StatSource.Veterancy },
                },
            });
            return unit;
        }

        private Entity Victim(byte owner, float3 position, int cost)
        {
            var victim = World.SpawnUnit(owner, position);
            World.SetCost(victim, _supplies, cost);
            return victim;
        }
    }
}
