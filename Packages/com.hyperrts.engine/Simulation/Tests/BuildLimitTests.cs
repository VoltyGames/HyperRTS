using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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
            _supplies = _world.CreateSupplies();
            _world.AddResources(1, _supplies, 1000);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity HeroPrefab(int max) =>
            _world.AddBuildLimit(_world.UnitPrefab(_supplies, 100, buildTime: 30f, name: "Hero"), max);

        private Entity LiveHero(byte faction, Entity prefab)
        {
            var hero = _world.SpawnUnit(faction, new float3(-20f, 0f, 0f), name: "Hero");
            return _world.AddBuildLimit(hero, _world.Get<BuildLimit>(prefab).Max);
        }

        [Test]
        public void Produce_StopsQueueingAtTheLimit_AndChargesOnlyWhatQueued()
        {
            var prefab = HeroPrefab(2);
            var producer = _world.SpawnProducer(prefab);

            for (var i = 0; i < 3; i++)
            {
                _world.Produce(1, producer, prefab);
            }

            _world.Tick();

            Assert.AreEqual(2, _world.QueueLength(producer));
            Assert.AreEqual(800, _world.GetResources(1, _supplies));
        }

        [Test]
        public void Produce_CountsOwnStandingUnits_NotOtherPlayers()
        {
            var prefab = HeroPrefab(2);
            var producer = _world.SpawnProducer(prefab);
            LiveHero(1, prefab);
            LiveHero(2, prefab);
            LiveHero(2, prefab);

            _world.Produce(1, producer, prefab);
            _world.Produce(1, producer, prefab);
            _world.Tick();

            Assert.AreEqual(1, _world.QueueLength(producer));
        }

        [Test]
        public void Produce_DeadUnitFreesItsSlot()
        {
            var prefab = HeroPrefab(1);
            var producer = _world.SpawnProducer(prefab);
            var hero = LiveHero(1, prefab);
            _world.EntityManager.SetComponentEnabled<Dead>(hero, true);

            _world.Produce(1, producer, prefab);
            _world.Tick();

            Assert.AreEqual(1, _world.QueueLength(producer));
        }

        [Test]
        public void PlaceBuilding_SecondSiteInTheSameFrameIsRefused()
        {
            var silo = _world.SpawnBuilding(0, Far, new float2(4f, 4f), name: "Silo");
            var prefab = _world.AddBuildLimit(_world.MakePrefab(silo), 1);
            _world.SetCost(prefab, _supplies, 100);
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero), prefab);

            _world.PlaceBuilding(builder, prefab, new float3(10f, 0f, 0f));
            _world.PlaceBuilding(builder, prefab, new float3(-10f, 0f, 0f));
            _world.Tick();
            _world.PlaceBuilding(builder, prefab, new float3(0f, 0f, 10f));
            _world.Tick();

            Assert.AreEqual(1, _world.All<BuildLimit>().Length);
            Assert.AreEqual(900, _world.GetResources(1, _supplies));
        }

        [Test]
        public void CanBuy_IsFalseAtTheLimit_ForTheHUD()
        {
            var prefab = HeroPrefab(1);
            Assert.IsTrue(CanBuy(prefab), "nothing owned yet");

            LiveHero(1, prefab);
            Assert.IsFalse(CanBuy(prefab), "the one allowed is standing");
        }

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
