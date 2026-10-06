using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Stealth and detection: hidden and untargetable unless detected, revealed by firing, with or without fog.</summary>
    public class StealthTests
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

        private Entity Stealthy(Entity entity, float reveal = 1f, bool onlyWhenStill = false, bool enabled = true)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            StealthSetup.AddStealth(ref writer,
                new Stealth { RevealDuration = reveal, OnlyWhenStill = onlyWhenStill }, enabled);
            return entity;
        }

        private Entity Detect(Entity entity, float radius)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            StealthSetup.AddDetector(ref writer, radius);
            return entity;
        }

        private bool IsHidden(Entity entity) => _world.EntityManager.HasComponent<FogHidden>(entity);

        private void DisableFog()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(MapSettings));
            var settings = query.GetSingleton<MapSettings>();
            settings.FogOfWar = false;
            query.SetSingleton(settings);
        }

        [Test]
        public void StealthedEnemy_IsNeitherAcquiredNorVisible()
        {
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            var enemy = Stealthy(_world.SpawnUnit(2, new float3(3f, 0f, 0f)));

            _world.Run(2f);

            Assert.AreEqual(100f, _world.HealthOf(enemy));
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier));
            Assert.IsTrue(IsHidden(enemy), "in sight but undetected");
        }

        [Test]
        public void Detector_RevealsStealthedEnemy_OnlyInRange()
        {
            var soldier = Detect(_world.AddWeapon(_world.SpawnUnit(1, float3.zero)), radius: 5f);
            var enemy = Stealthy(_world.SpawnUnit(2, new float3(8f, 0f, 0f)));

            _world.Run(1f);
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier), "inside vision, outside detection");
            Assert.IsTrue(IsHidden(enemy));

            _world.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(new float3(3f, 0f, 0f)));
            _world.Run(0.3f);
            Assert.IsFalse(IsHidden(enemy));

            _world.Run(3f);
            Assert.IsFalse(_world.EntityManager.Exists(enemy), "detected, so acquired and killed");
        }

        [Test]
        public void Firing_RevealsShooter_ThenItRestealths()
        {
            var shooter = Stealthy(_world.AddWeapon(_world.SpawnUnit(2, float3.zero), cooldown: 100f), reveal: 1f);
            var target = _world.SpawnUnit(1, new float3(3f, 0f, 0f), maxHealth: 1000f);

            _world.Run(0.4f);
            Assert.Less(_world.HealthOf(target), 1000f, "fired once");
            Assert.IsFalse(_world.IsEnabled<Stealthed>(shooter));
            Assert.IsFalse(IsHidden(shooter), "visible while revealed");

            _world.Run(1.2f);
            Assert.IsTrue(_world.IsEnabled<Stealthed>(shooter));
            Assert.IsTrue(IsHidden(shooter), "hidden again once the reveal ends");
        }

        [Test]
        public void FogOff_StillHidesStealthedEnemies_UntilDetected()
        {
            DisableFog();
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            var enemy = Stealthy(_world.SpawnUnit(2, new float3(3f, 0f, 0f)));
            var far = _world.SpawnUnit(2, new float3(80f, 0f, 0f));

            _world.Run(1f);
            Assert.AreEqual(100f, _world.HealthOf(enemy));
            Assert.IsTrue(IsHidden(enemy));
            Assert.IsFalse(IsHidden(far), "fog off shows everything else");

            Detect(soldier, 5f);
            _world.Run(1f);
            Assert.IsFalse(IsHidden(enemy));
            Assert.Less(_world.HealthOf(enemy), 100f);
        }

        [Test]
        public void StealthedTarget_CannotBeOrderedAttacked()
        {
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            var enemy = Stealthy(_world.SpawnUnit(2, new float3(15f, 0f, 0f)));
            _world.Tick();

            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.Smart, Unit = soldier, Target = enemy, Position = new float3(15f, 0f, 0f),
            });
            _world.Tick();

            Assert.AreEqual(OrderType.Move, _world.Get<ActiveOrder>(soldier).Value.Type);
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier));
        }

        [Test]
        public void StealthDisabledOrMoving_LeavesUnitVisible()
        {
            var toggled = Stealthy(_world.SpawnUnit(2, new float3(3f, 0f, 0f)), enabled: false);
            var still = Stealthy(_world.SpawnUnit(2, new float3(-3f, 0f, 0f)), onlyWhenStill: true);
            _world.Tick();
            Assert.IsFalse(_world.IsEnabled<Stealthed>(toggled));
            Assert.IsTrue(_world.IsEnabled<Stealthed>(still));

            _world.EntityManager.SetComponentEnabled<Stealth>(toggled, true);
            _world.EntityManager.SetComponentData(still, new MoveDestination { Value = new float3(-40f, 0f, 0f) });
            _world.EntityManager.SetComponentEnabled<MoveDestination>(still, true);
            _world.Tick();

            Assert.IsTrue(_world.IsEnabled<Stealthed>(toggled), "games toggle stealth at runtime");
            Assert.IsFalse(_world.IsEnabled<Stealthed>(still), "still-only stealth drops while moving");
        }
    }
}
