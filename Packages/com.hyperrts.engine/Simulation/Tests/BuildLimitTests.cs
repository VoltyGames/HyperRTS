using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Per-player build limits on queued, placed and standing units and buildings.</summary>
    public class BuildLimitTests
    {
        private static readonly float3 Far = new(90f, 0f, 90f);

        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = ScriptableObject.CreateInstance<ResourceType>();
            _world.AddResources(1, _supplies, 1000);
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_supplies);
        }

        private Entity UnitPrefab(int max)
        {
            var prefab = _world.AddBuildLimit(_world.MakePrefab(_world.SpawnUnit(0, Far, name: "Hero")), max);
            _world.SetBuildTime(prefab, 30f);
            _world.SetCost(prefab, _supplies, 100);
            return prefab;
        }

        private Entity Producer(Entity prefab) =>
            _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), new float3(0f, 0f, -4f),
                prefab);

        private Entity LiveHero(byte faction, Entity prefab)
        {
            var hero = _world.SpawnUnit(faction, new float3(-20f, 0f, 0f), name: "Hero");
            return _world.AddBuildLimit(hero, _world.Get<BuildLimit>(prefab).Max);
        }

        private int QueueLength(Entity producer) => _world.EntityManager.GetBuffer<ProductionQueueItem>(producer).Length;

        [Test]
        public void Produce_StopsQueueingAtTheLimit_AndChargesOnlyWhatQueued()
        {
            var prefab = UnitPrefab(2);
            var producer = Producer(prefab);

            for (var i = 0; i < 3; i++)
            {
                _world.Produce(1, producer, prefab);
            }

            _world.Tick();

            Assert.AreEqual(2, QueueLength(producer));
            Assert.AreEqual(800, _world.ResourcesOf(1, _supplies));
        }

        [Test]
        public void Produce_CountsOwnStandingUnits_NotOtherPlayers()
        {
            var prefab = UnitPrefab(2);
            var producer = Producer(prefab);
            LiveHero(1, prefab);
            LiveHero(2, prefab);
            LiveHero(2, prefab);

            _world.Produce(1, producer, prefab);
            _world.Produce(1, producer, prefab);
            _world.Tick();

            Assert.AreEqual(1, QueueLength(producer));
        }

        [Test]
        public void Produce_DeadUnitFreesItsSlot()
        {
            var prefab = UnitPrefab(1);
            var producer = Producer(prefab);
            var hero = LiveHero(1, prefab);
            _world.EntityManager.SetComponentEnabled<Dead>(hero, true);

            _world.Produce(1, producer, prefab);
            _world.Tick();

            Assert.AreEqual(1, QueueLength(producer));
        }

        [Test]
        public void PlaceBuilding_SecondSiteInTheSameFrameIsRefused()
        {
            var silo = _world.SpawnBuilding(0, Far, new float2(4f, 4f), name: "Silo");
            var prefab = _world.AddBuildLimit(_world.MakePrefab(silo), 1);
            _world.SetCost(prefab, _supplies, 100);
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero), prefab);

            Place(builder, prefab, new float3(10f, 0f, 0f));
            Place(builder, prefab, new float3(-10f, 0f, 0f));
            _world.Tick();
            Place(builder, prefab, new float3(0f, 0f, 10f));
            _world.Tick();

            Assert.AreEqual(1, _world.All<BuildLimit>().Length);
            Assert.AreEqual(900, _world.ResourcesOf(1, _supplies));
        }

        [Test]
        public void CanBuy_IsFalseAtTheLimit_ForTheHUD()
        {
            var prefab = UnitPrefab(1);
            Assert.IsTrue(CanBuy(prefab), "nothing owned yet");

            LiveHero(1, prefab);
            Assert.IsFalse(CanBuy(prefab), "the one allowed is standing");
        }

        private void Place(Entity builder, Entity prefab, float3 position) =>
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = position,
            });

        private bool CanBuy(Entity prefab)
        {
            var entityManager = _world.EntityManager;
            using var queues = ResearchRules.QueueQuery(Allocator.Temp).Build(entityManager);
            using var limited = BuildCounts.Query(Allocator.Temp).Build(entityManager);
            using var completed = CompletedBuildings.Query(Allocator.Temp).Build(entityManager);
            var player = _world.Player(1);
            var buyer = Buyer.Snapshot(entityManager, queues, limited, 1,
                new CompletedBuildings(completed, Allocator.Temp), entityManager.GetBuffer<CompletedResearch>(player));
            var stock = entityManager.GetBuffer<ResourceStock>(player);
            return PurchaseRules.CanBuy(entityManager, prefab, buyer, stock.AsNativeArray());
        }
    }
}
