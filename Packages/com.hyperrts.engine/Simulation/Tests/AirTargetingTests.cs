using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Weapon target layers: surface-only guns ignore aircraft, anti-air weapons engage them.</summary>
    public class AirTargetingTests
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
        public void SurfaceOnlyGun_IgnoresAircraft_AndCantBeOrderedAtThem()
        {
            var gun = _world.Arm(_world.SpawnUnit(1, float3.zero), range: 6f);
            var aircraft = _world.SpawnAircraft(2, new float3(3f, 0f, 0f));
            _world.Run(2f);

            Assert.AreEqual(100f, _world.HealthOf(aircraft), "never auto-acquired");
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(gun));

            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.Smart, Unit = gun, Target = aircraft, Position = new float3(3f, 0f, 0f),
            });
            _world.Tick();

            Assert.AreEqual(OrderType.Move, _world.Get<ActiveOrder>(gun).Value.Type, "right-click just moves there");
        }

        [Test]
        public void AntiAirWeapon_EngagesAircraft_AndIgnoresGround()
        {
            var launcher = _world.Arm(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f,
                targets: WeaponTargets.Air);
            var tank = _world.SpawnUnit(2, new float3(-2f, 0f, 0f));
            var aircraft = _world.SpawnAircraft(2, new float3(3f, 0f, 0f));
            _world.Run(2f);

            Assert.Less(_world.HealthOf(aircraft), 100f);
            Assert.AreEqual(100f, _world.HealthOf(tank));
            Assert.AreEqual(aircraft, _world.Get<AttackTarget>(launcher).Value);
        }

        [Test]
        public void GroundSplash_SparesTheAircraftAbove()
        {
            var gun = _world.SpawnUnit(1, float3.zero);
            var sink = new EntityManagerSink(_world.EntityManager, gun);
            WeaponSetup.Add(ref sink, new Weapon
            {
                Range = 8f, Damage = 30f, Cooldown = 100f, SplashRadius = 3f, SplashEdgeFactor = 1f,
                Targets = WeaponTargets.Surface,
            }, Stance.Aggressive, float3.zero);
            var tank = _world.SpawnUnit(2, new float3(6f, 0f, 0f));
            var aircraft = _world.SpawnAircraft(2, new float3(6f, 0f, 1f));

            _world.Run(1f);

            Assert.AreEqual(70f, _world.HealthOf(tank), 1e-3f);
            Assert.AreEqual(100f, _world.HealthOf(aircraft), "splash stays on the surface");
        }
    }
}
