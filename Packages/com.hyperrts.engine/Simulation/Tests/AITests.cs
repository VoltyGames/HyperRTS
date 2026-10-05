using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
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
            _world.EntityManager.AddComponentData(_world.Player(2),
                new AIPlayer { ThinkInterval = 0.5f, AttackWaveSize = 2 });
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
            var sink = new EntityManagerSink(_world.EntityManager, prefab);
            WeaponSetup.Add(ref sink, new Weapon { Range = 2f, Damage = 1f, Cooldown = 1f }, Stance.Aggressive,
                float3.zero);
            return prefab;
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
            _world.Give(2, _supplies, 100);

            _world.Run(3f);

            var attacking = 0;
            foreach (var unit in _world.All<Weapon>())
            {
                var order = _world.Get<ActiveOrder>(unit).Value;
                if (_world.IsEnabled<ActiveOrder>(unit) && order.Type == OrderType.AttackMove &&
                    math.distance(order.Position, new float3(60f, 0f, 0f)) < 5f)
                {
                    attacking++;
                }
            }

            Assert.GreaterOrEqual(attacking, 2, "a wave of trained soldiers was sent at the enemy base");
            Assert.Less(_world.Stock(2, _supplies), 100, "training was paid for");
        }
    }
}
