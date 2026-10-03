using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Targeting, stances, attack orders, projectiles and armor, run end to end through the systems.</summary>
    public class CombatTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2, 1);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity Arm(Entity entity, Stance stance = Stance.Aggressive, float range = 4f, float damage = 25f,
            float cooldown = 0.5f, Entity projectile = default, UnityObjectRef<DamageType> damageType = default)
        {
            var weapon = new Weapon
            {
                Range = range,
                Damage = damage,
                Cooldown = cooldown,
                ProjectilePrefab = projectile,
                ProjectileSpeed = 10f,
                DamageType = damageType,
            };
            var sink = new EntityManagerSink(_world.EntityManager, entity);
            WeaponSetup.Add(ref sink, weapon, stance, _world.Get<LocalTransform>(entity).Position);
            return entity;
        }

        private float HealthOf(Entity entity) => _world.Get<Health>(entity).Current;

        [Test]
        public void IdleArmedUnit_AcquiresAndKillsNearbyEnemy()
        {
            var soldier = Arm(_world.SpawnUnit(1, float3.zero));
            var enemy = _world.SpawnUnit(2, new float3(8f, 0f, 0f));

            _world.Run(5f);

            Assert.IsFalse(_world.EntityManager.Exists(enemy));
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier), "drops the target once it is gone");
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(soldier), "stops where it won");
        }

        [Test]
        public void PassiveStance_NeverEngages()
        {
            var soldier = Arm(_world.SpawnUnit(1, float3.zero), Stance.Passive);
            var enemy = _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            _world.Run(3f);

            Assert.AreEqual(100f, HealthOf(enemy));
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier));
        }

        [Test]
        public void Allies_AreNeverTargeted()
        {
            Arm(_world.SpawnUnit(1, float3.zero));
            var friend = _world.SpawnUnit(1, new float3(2f, 0f, 0f));
            var ally = _world.SpawnUnit(3, new float3(-2f, 0f, 0f));

            _world.Run(3f);

            Assert.AreEqual(100f, HealthOf(friend));
            Assert.AreEqual(100f, HealthOf(ally));
        }

        [Test]
        public void AttackOrder_ChasesBeyondAcquireRange_ThenCompletes()
        {
            var soldier = Arm(_world.SpawnUnit(1, float3.zero));
            var enemy = _world.SpawnUnit(2, new float3(25f, 0f, 0f), speed: 2f);
            _world.EntityManager.SetComponentData(enemy, new MoveDestination { Value = new float3(80f, 0f, 0f) });
            _world.EntityManager.SetComponentEnabled<MoveDestination>(enemy, true);
            _world.EntityManager.SetComponentData(soldier, new ActiveOrder
            {
                Value = new Order { Type = OrderType.Attack, Target = enemy },
            });
            _world.EntityManager.SetComponentEnabled<ActiveOrder>(soldier, true);

            _world.Run(15f);

            Assert.IsFalse(_world.EntityManager.Exists(enemy));
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(soldier), "the attack order completes");
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier));
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(soldier));
        }

        [Test]
        public void HoldPosition_FiresInRangeButNeverMoves()
        {
            var soldier = Arm(_world.SpawnUnit(1, float3.zero), Stance.HoldPosition);
            var near = _world.SpawnUnit(2, new float3(3f, 0f, 0f));
            var far = _world.SpawnUnit(2, new float3(0f, 0f, 8f));

            _world.Run(5f);

            Assert.IsFalse(_world.EntityManager.Exists(near));
            Assert.AreEqual(100f, HealthOf(far), "out of weapon range");
            Assert.AreEqual(0f, math.length(_world.Get<LocalTransform>(soldier).Position.xz), 1e-4f);
        }

        [Test]
        public void Projectile_LandsAfterFiring_AndArmorScalesDamage()
        {
            var bullet = ScriptableObject.CreateInstance<DamageType>();
            var prefab = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(prefab, LocalTransform.Identity);
            _world.MakePrefab(prefab);

            Arm(_world.SpawnUnit(1, float3.zero), range: 8f, damage: 40f, cooldown: 100f, projectile: prefab,
                damageType: bullet);
            var tank = _world.SpawnUnit(2, new float3(6f, 0f, 0f));
            _world.EntityManager.AddBuffer<ArmorModifier>(tank)
                .Add(new ArmorModifier { DamageType = bullet, Multiplier = 0.5f });

            using var projectiles = _world.EntityManager.CreateEntityQuery(typeof(Projectile));
            for (var frame = 0; frame < 30 && projectiles.IsEmpty; frame++)
            {
                _world.Tick();
            }

            Assert.IsFalse(projectiles.IsEmpty, "a projectile was launched");
            Assert.AreEqual(100f, HealthOf(tank), "no damage until the projectile arrives");

            _world.Run(2f);

            Assert.AreEqual(80f, HealthOf(tank), "40 damage halved by armor");
            Assert.IsTrue(projectiles.IsEmpty, "the projectile is destroyed on impact");
            Object.DestroyImmediate(bullet);
        }

        [Test]
        public void TowerUnderConstruction_HoldsFireUntilFinished()
        {
            var tower = Arm(_world.SpawnBuilding(1, float3.zero, new float2(2f, 2f), complete: false, buildTime: 100f));
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f));

            _world.Run(2f);
            Assert.AreEqual(100f, HealthOf(enemy));

            _world.EntityManager.SetComponentEnabled<ConstructionProgress>(tower, false);
            _world.Run(2f);
            Assert.IsFalse(_world.EntityManager.Exists(enemy), "the finished tower opens fire");
        }
    }
}
