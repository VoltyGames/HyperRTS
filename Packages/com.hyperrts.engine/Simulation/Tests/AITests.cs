using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Skirmish AI: it plays through the same commands as a human.</summary>
    public class AITests
    {
        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = ScriptableObject.CreateInstance<ResourceType>();
            var writer = new EntityManagerWriter(_world.EntityManager, _world.Player(2));
            AIPlayerSetup.Add(ref writer,
                new AIPlayer { ThinkInterval = 0.5f, AttackWaveSize = 2, UseAbilities = true });
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_supplies);
        }

        private Entity SoldierPrefab()
        {
            var prefab = _world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 90f)));
            _world.SetBuildTime(prefab, 0.5f);
            _world.SetCost(prefab, _supplies, 10);
            var writer = new EntityManagerWriter(_world.EntityManager, prefab);
            WeaponSetup.Add(ref writer, new Weapon { Range = 2f, Damage = 1f, Cooldown = 1f }, Stance.Aggressive,
                float3.zero);
            return prefab;
        }

        private Entity GiveAbility(Entity entity, string name, AbilityTarget target, float range, float damage,
            float radius = 0f)
        {
            _world.EntityManager.AddBuffer<Ability>(entity).Add(new Ability
            {
                Id = EntityInfo.TypeIdFromName(name), Name = name, Target = target,
                Filter = AbilityTargetFilter.Hostile, Range = range, Cooldown = 30f, Damage = damage,
                Radius = radius,
            });
            return entity;
        }

        [Test]
        public void AI_SendsIdleHarvesterToNearestNode()
        {
            var harvester = _world.MakeHarvester(_world.SpawnUnit(2, float3.zero), 10, 1f);
            _world.SpawnNode(new float3(30f, 0f, 0f), _supplies, 100);
            var near = _world.SpawnNode(new float3(5f, 0f, 0f), _supplies, 100);

            _world.Tick();

            Assert.IsTrue(_world.IsEnabled<ActiveOrder>(harvester));
            Assert.AreEqual(OrderType.Gather, _world.Get<ActiveOrder>(harvester).Value.Type);
            Assert.AreEqual(near, _world.Get<ActiveOrder>(harvester).Value.Target);
        }

        [Test]
        public void AI_TrainsUnits_ThenAttacksTheEnemyBase()
        {
            var soldier = SoldierPrefab();
            _world.MakeProducer(_world.SpawnBuilding(2, float3.zero, new float2(4f, 4f)), new float3(0f, 0f, -4f),
                soldier);
            _world.SpawnProvider(2, new float3(-20f, 0f, 0f), 10);
            _world.SpawnBuilding(1, new float3(60f, 0f, 0f), new float2(4f, 4f));
            _world.AddResources(2, _supplies, 100);

            _world.Run(3f);

            var attacking = 0;
            foreach (var unit in _world.All<Weapon>())
            {
                var order = _world.Get<ActiveOrder>(unit).Value;
                var attackMoving = _world.IsEnabled<ActiveOrder>(unit) && order.Type == OrderType.AttackMove;
                if (attackMoving && math.distance(order.Position, new float3(60f, 0f, 0f)) < 5f)
                {
                    attacking++;
                }
            }

            Assert.GreaterOrEqual(attacking, 2, "a wave of trained soldiers was sent at the enemy base");
            Assert.Less(_world.ResourcesOf(2, _supplies), 100, "training was paid for");
        }

        [Test]
        public void AI_BudgetsItsStockpile_AcrossIdleProducers()
        {
            var soldier = SoldierPrefab();
            var tank = _world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 80f), name: "Tank"));
            _world.SetCost(tank, _supplies, 15);
            _world.SetBuildTime(soldier, 30f);
            _world.SetBuildTime(tank, 30f);
            var first = _world.MakeProducer(_world.SpawnBuilding(2, float3.zero, new float2(4f, 4f)),
                new float3(0f, 0f, -4f), soldier, tank);
            var second = _world.MakeProducer(_world.SpawnBuilding(2, new float3(10f, 0f, 0f), new float2(4f, 4f)),
                new float3(0f, 0f, -4f), soldier, tank);
            _world.SpawnProvider(2, new float3(-20f, 0f, 0f), 10);
            _world.AddResources(2, _supplies, 20);

            _world.Tick(frames: 3);

            // With the soldier's cost set aside, the tank is out of reach and the second producer trains a soldier too.
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<ProductionQueueItem>(first).Length);
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<ProductionQueueItem>(second).Length);
            Assert.AreEqual(0, _world.ResourcesOf(2, _supplies));
        }

        [Test]
        public void AI_FiresReadyAbility_AtNearestEnemyInRange()
        {
            var caster = GiveAbility(_world.SpawnUnit(2, float3.zero, speed: 0f), "Snipe", AbilityTarget.Entity, 8f, 30f);
            var far = _world.SpawnUnit(1, new float3(-20f, 0f, 0f), speed: 0f);
            var near = _world.SpawnUnit(1, new float3(6f, 0f, 0f), speed: 0f);

            _world.Run(1f);

            Assert.AreEqual(70f, _world.Get<Health>(near).Current, 1e-3f, "the enemy in range was hit");
            Assert.AreEqual(100f, _world.Get<Health>(far).Current, 1e-3f, "the one out of range was not");
            Assert.Greater(_world.EntityManager.GetBuffer<Ability>(caster)[0].CooldownRemaining, 0f);
        }

        [Test]
        public void AI_FiresPlayerPower_AtTheEnemyAnywhere()
        {
            _world.SpawnBuilding(2, float3.zero, new float2(4f, 4f));
            var enemy = _world.SpawnBuilding(1, new float3(80f, 0f, 80f), new float2(4f, 4f));
            GiveAbility(_world.Player(2), "Strike", AbilityTarget.Point, 0f, 40f, radius: 3f);

            _world.Run(1f);

            Assert.AreEqual(460f, _world.Get<Health>(enemy).Current, 1e-3f);
        }
    }
}
