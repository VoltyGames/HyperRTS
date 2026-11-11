using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Stats;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>The damage queue (splash, friendly fire, armor, heals) and splash weapons.</summary>
    public class DamageTests : SimulationFixture
    {
        [Test]
        public void Splash_FallsOffWithDistance_AndSparesAllies()
        {
            var centre = World.SpawnUnit(2, float3.zero, new TestUnit { Radius = 0f });
            var edge = World.SpawnUnit(2, new float3(4f, 0f, 0f), new TestUnit { Radius = 0f });
            var outside = World.SpawnUnit(2, new float3(6f, 0f, 0f), new TestUnit { Radius = 0f });
            var friend = World.SpawnUnit(1, new float3(1f, 0f, 0f), new TestUnit { Radius = 0f });
            World.Tick();

            World.QueueDamage(Blast(float3.zero, 4f));
            World.Tick();

            Assert.AreEqual(60f, World.GetHealth(centre), 1e-3f, "full damage at the centre");
            Assert.AreEqual(80f, World.GetHealth(edge), 1e-3f, "edge factor at the radius");
            Assert.AreEqual(100f, World.GetHealth(outside));
            Assert.AreEqual(100f, World.GetHealth(friend), "splash spares allies by default");
        }

        [Test]
        public void FriendlyFire_HurtsAllies()
        {
            var friend = World.SpawnUnit(1, float3.zero, new TestUnit { Radius = 0f });
            World.Tick();

            World.QueueDamage(Blast(float3.zero, 2f, friendlyFire: true));
            World.Tick();

            Assert.AreEqual(60f, World.GetHealth(friend), 1e-3f);
        }

        [TestCase(0f, 0f, 10f, 0.5f)]
        [TestCase(10f, 0f, 0f, 1f)]
        [TestCase(0f, 0f, -10f, 2f)]
        public void DirectionalArmor_ScalesByHitSide(float x, float y, float z, float multiplier)
        {
            var tank = World.SpawnUnit(2, float3.zero);
            var writer = World.Writer(tank);
            writer.Add(new ArmorFacing { Front = 0.5f, Side = 1f, Rear = 2f });

            World.QueueDamage(new DamageEvent { Target = tank, Origin = new float3(x, y, z), SourceOwner = 1, Profile = new DamageProfile { Amount = 20f } });
            World.Tick();

            Assert.AreEqual(100f - 20f * multiplier, World.GetHealth(tank), 1e-3f);
        }

        [Test]
        public void DamageTakenStat_AndKillCredit_AreApplied()
        {
            var shooter = World.SpawnUnit(1, new float3(-5f, 0f, 0f));
            var target = World.SpawnUnit(2, float3.zero);
            var source = new StatSource(StatSourceKind.Upgrade, 99);
            World.Buffer<StatModifier>(target)
                .Add(new StatModifier { Stat = Stat.DamageTaken, Percent = -0.25f, Source = source });

            World.QueueDamage(new DamageEvent { Target = target, Source = shooter, SourceOwner = 1, Profile = new DamageProfile { Amount = 40f } });
            World.Tick();

            Assert.AreEqual(70f, World.GetHealth(target), 1e-3f);
            Assert.AreEqual(shooter, World.Get<LastAttacker>(target).Source);
        }

        [Test]
        public void NegativeDamage_HealsUpToMax()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            World.Set(unit, new Health { Current = 50f, Max = 100f });

            World.QueueDamage(new DamageEvent { Target = unit, Profile = new DamageProfile { Amount = -80f } });
            World.Tick();

            Assert.AreEqual(100f, World.GetHealth(unit));
        }

        [Test]
        public void SplashWeapon_HitsTargetAndNeighbours()
        {
            var gun = World.SpawnUnit(1, float3.zero);
            var writer = World.Writer(gun);
            var weapon = new Weapon
            {
                Range = 8f, Damage = new DamageProfile { Amount = 30f, Radius = 3f, EdgeFactor = 1f }, Cooldown = 100f,
                Targets = WeaponTargets.Surface,
            };
            WeaponSetup.Add(writer, new[] { WeaponMount.Create(weapon, default) }, CombatStance.Create(Stance.Aggressive, float3.zero));
            var target = World.SpawnUnit(2, new float3(6f, 0f, 0f));
            var neighbour = World.SpawnUnit(2, new float3(6f, 0f, 1.5f));
            var aircraft = World.SpawnAircraft(2, new float3(6f, 0f, -1f));

            World.Run(1f);

            Assert.AreEqual(70f, World.GetHealth(target), 1e-3f);
            Assert.AreEqual(70f, World.GetHealth(neighbour), 1e-3f);
            Assert.AreEqual(100f, World.GetHealth(aircraft), "splash stays on the surface");
            Assert.AreEqual(0f, math.length(World.Get<LocalTransform>(gun).Position.xz), 1e-3f);
        }

        private static DamageEvent Blast(float3 position, float radius, bool friendlyFire = false) => new()
        {
            Position = position,
            Origin = position,
            Layers = WeaponTargets.Surface,
            SourceOwner = 1,
            Profile = new DamageProfile { Amount = 40f, Radius = radius, EdgeFactor = 0.5f, FriendlyFire = friendlyFire },
        };
    }
}
