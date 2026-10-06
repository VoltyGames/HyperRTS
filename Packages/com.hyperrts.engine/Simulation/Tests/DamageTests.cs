using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Stats;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The damage queue: splash falloff, friendly fire, directional armor, the DamageTaken stat, heals.</summary>
    public class DamageTests
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

        private void Queue(DamageEvent hit)
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(DamageQueue));
            _world.EntityManager.GetBuffer<DamageEvent>(query.GetSingletonEntity()).Add(hit);
        }

        private static DamageEvent Blast(float3 position, float radius, bool friendlyFire = false) => new()
        {
            Position = position,
            Origin = position,
            SourceFaction = 1,
            Amount = 40f,
            Radius = radius,
            EdgeFactor = 0.5f,
            FriendlyFire = friendlyFire,
        };

        [Test]
        public void Splash_FallsOffWithDistance_AndSparesAllies()
        {
            var centre = _world.SpawnUnit(2, float3.zero, radius: 0f);
            var edge = _world.SpawnUnit(2, new float3(4f, 0f, 0f), radius: 0f);
            var outside = _world.SpawnUnit(2, new float3(6f, 0f, 0f), radius: 0f);
            var friend = _world.SpawnUnit(1, new float3(1f, 0f, 0f), radius: 0f);
            _world.Tick();

            Queue(Blast(float3.zero, 4f));
            _world.Tick();

            Assert.AreEqual(60f, _world.HealthOf(centre), 1e-3f, "full damage at the centre");
            Assert.AreEqual(80f, _world.HealthOf(edge), 1e-3f, "edge factor at the radius");
            Assert.AreEqual(100f, _world.HealthOf(outside));
            Assert.AreEqual(100f, _world.HealthOf(friend), "splash spares allies by default");
        }

        [Test]
        public void FriendlyFire_HurtsAllies()
        {
            var friend = _world.SpawnUnit(1, float3.zero, radius: 0f);
            _world.Tick();

            Queue(Blast(float3.zero, 2f, friendlyFire: true));
            _world.Tick();

            Assert.AreEqual(60f, _world.HealthOf(friend), 1e-3f);
        }

        [TestCase(0f, 0f, 10f, 0.5f)]
        [TestCase(10f, 0f, 0f, 1f)]
        [TestCase(0f, 0f, -10f, 2f)]
        public void DirectionalArmor_ScalesByHitSide(float x, float y, float z, float multiplier)
        {
            var tank = _world.SpawnUnit(2, float3.zero);
            var sink = new EntityManagerSink(_world.EntityManager, tank);
            ArmorSetup.AddFacing(ref sink, new ArmorFacing { Front = 0.5f, Side = 1f, Rear = 2f });

            Queue(new DamageEvent { Target = tank, Origin = new float3(x, y, z), SourceFaction = 1, Amount = 20f });
            _world.Tick();

            Assert.AreEqual(100f - 20f * multiplier, _world.HealthOf(tank), 1e-3f);
        }

        [Test]
        public void DamageTakenStat_AndKillCredit_AreApplied()
        {
            var shooter = _world.SpawnUnit(1, new float3(-5f, 0f, 0f));
            var target = _world.SpawnUnit(2, float3.zero);
            var source = new StatSource(StatSourceKind.Custom, 99);
            _world.EntityManager.GetBuffer<StatModifier>(target)
                .Add(new StatModifier { Stat = Stat.DamageTaken, Percent = -0.25f, Source = source });

            Queue(new DamageEvent { Target = target, Source = shooter, SourceFaction = 1, Amount = 40f });
            _world.Tick();

            Assert.AreEqual(70f, _world.HealthOf(target), 1e-3f);
            Assert.AreEqual(shooter, _world.Get<LastAttacker>(target).Source);
        }

        [Test]
        public void NegativeDamage_HealsUpToMax()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            _world.EntityManager.SetComponentData(unit, new Health { Current = 50f, Max = 100f });

            Queue(new DamageEvent { Target = unit, Amount = -80f });
            _world.Tick();

            Assert.AreEqual(100f, _world.HealthOf(unit));
        }

        [Test]
        public void SplashWeapon_HitsTargetAndNeighbours()
        {
            var gun = _world.SpawnUnit(1, float3.zero);
            var sink = new EntityManagerSink(_world.EntityManager, gun);
            WeaponSetup.Add(ref sink, new Weapon
            {
                Range = 8f, Damage = 30f, Cooldown = 100f, SplashRadius = 3f, SplashEdgeFactor = 1f,
            }, Stance.Aggressive, float3.zero);
            var target = _world.SpawnUnit(2, new float3(6f, 0f, 0f));
            var neighbour = _world.SpawnUnit(2, new float3(6f, 0f, 1.5f));

            _world.Run(1f);

            Assert.AreEqual(70f, _world.HealthOf(target), 1e-3f);
            Assert.AreEqual(70f, _world.HealthOf(neighbour), 1e-3f);
            Assert.AreEqual(0f, math.length(_world.Get<LocalTransform>(gun).Position.xz), 1e-3f);
        }
    }
}
