using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Building placement, construction, production queues, rally points and prerequisites.</summary>
    public class ProductionTests
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
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_supplies);
        }

        private Entity BuildingPrefab(int cost)
        {
            var prefab = _world.MakePrefab(_world.SpawnBuilding(0, Far, new float2(4f, 4f), buildTime: 2f,
                name: "Barracks"));
            _world.SetCost(prefab, _supplies, cost);
            return prefab;
        }

        private Entity UnitPrefab(int cost, float buildTime = 1f)
        {
            var prefab = _world.MakePrefab(_world.SpawnUnit(0, Far));
            _world.SetBuildTime(prefab, buildTime);
            _world.SetCost(prefab, _supplies, cost);
            return prefab;
        }

        private Entity Producer(Entity unitPrefab, bool complete = true) =>
            _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f), complete),
                new float3(0f, 0f, -4f), unitPrefab);

        private void Place(Entity builder, Entity prefab, float3 position) =>
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = position,
            });

        private int QueueLength(Entity producer) => _world.EntityManager.GetBuffer<ProductionQueueItem>(producer).Length;

        [Test]
        public void PlaceBuilding_SpendsCost_AndBuilderFinishesConstruction()
        {
            var prefab = BuildingPrefab(50);
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero), prefab);
            _world.Give(1, _supplies, 100);

            Place(builder, prefab, new float3(6.3f, 0f, 0.2f));
            _world.Tick();

            Assert.AreEqual(50, _world.Stock(1, _supplies));
            var sites = _world.All<BuildingTag>();
            Assert.AreEqual(1, sites.Length);
            var site = sites[0];
            Assert.AreEqual(new float3(6f, 0f, 0f), _world.Get<LocalTransform>(site).Position, "snapped to the grid");
            Assert.AreEqual(1, _world.Get<Faction>(site).Value);
            Assert.IsTrue(_world.IsEnabled<ConstructionProgress>(site));
            Assert.AreEqual(OrderType.Build, _world.Get<ActiveOrder>(builder).Value.Type);

            _world.Run(4f);

            Assert.IsFalse(_world.IsEnabled<ConstructionProgress>(site));
            Assert.AreEqual(1f, _world.Get<ConstructionProgress>(site).Value);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(builder), "the Build order completes with the site");
        }

        [Test]
        public void ConstructionSite_DoesNotProgressWithoutBuilders()
        {
            var site = _world.SpawnBuilding(1, float3.zero, new float2(4f, 4f), complete: false, buildTime: 1f);

            _world.Run(3f);

            Assert.IsTrue(_world.IsEnabled<ConstructionProgress>(site));
            Assert.AreEqual(0f, _world.Get<ConstructionProgress>(site).Value);
        }

        [Test]
        public void PlaceBuilding_RejectedOnOccupiedGround()
        {
            var prefab = BuildingPrefab(50);
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero), prefab);
            _world.SpawnBuilding(1, new float3(6f, 0f, 0f), new float2(4f, 4f));
            _world.Give(1, _supplies, 100);

            Place(builder, prefab, new float3(7f, 0f, 1f));
            _world.Tick();

            Assert.AreEqual(100, _world.Stock(1, _supplies));
            Assert.AreEqual(1, _world.All<BuildingTag>().Length);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(builder));
        }

        [Test]
        public void PlaceBuilding_RejectedWithoutMoney()
        {
            var prefab = BuildingPrefab(50);
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero), prefab);
            _world.Give(1, _supplies, 40);

            Place(builder, prefab, new float3(6f, 0f, 0f));
            _world.Tick();

            Assert.AreEqual(40, _world.Stock(1, _supplies));
            Assert.AreEqual(0, _world.All<BuildingTag>().Length);
        }

        [Test]
        public void Produce_SpendsCost_AndSpawnsUnitAfterBuildTime()
        {
            var prefab = UnitPrefab(30);
            var producer = Producer(prefab);
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            _world.Give(1, _supplies, 100);

            _world.Produce(1, producer, prefab);
            _world.Tick();
            Assert.AreEqual(70, _world.Stock(1, _supplies));
            Assert.AreEqual(1, QueueLength(producer));

            _world.Run(0.5f);
            Assert.AreEqual(0, _world.All<UnitTag>().Length, "still training");

            _world.Run(1f);
            var units = _world.All<UnitTag>();
            Assert.AreEqual(1, units.Length);
            Assert.AreEqual(new float3(0f, 0f, -4f), _world.Get<LocalTransform>(units[0]).Position);
            Assert.AreEqual(1, _world.Get<Faction>(units[0]).Value);
            Assert.AreEqual(0, QueueLength(producer));
        }

        [Test]
        public void CancelProduction_RefundsCost()
        {
            var prefab = UnitPrefab(30);
            var producer = Producer(prefab);
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            _world.Give(1, _supplies, 100);

            _world.Produce(1, producer, prefab);
            _world.Produce(1, producer, prefab);
            _world.Tick();
            Assert.AreEqual(40, _world.Stock(1, _supplies));

            _world.Command(1, new PlayerCommand { Type = CommandType.CancelProduction, Unit = producer, Argument = -1 });
            _world.Tick();

            Assert.AreEqual(70, _world.Stock(1, _supplies));
            Assert.AreEqual(1, QueueLength(producer));
        }

        [Test]
        public void PopulationCap_BlocksProductionUntilRaised()
        {
            var prefab = UnitPrefab(10);
            var producer = Producer(prefab);
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 1);
            _world.SpawnUnit(1, new float3(10f, 0f, 10f));
            _world.Give(1, _supplies, 100);

            _world.Produce(1, producer, prefab);
            _world.Run(3f);
            Assert.AreEqual(1, _world.All<UnitTag>().Length, "the cap is full");
            Assert.AreEqual(1, QueueLength(producer));

            _world.SpawnProvider(1, new float3(-20f, 0f, 20f), 5);
            _world.Run(1.5f);
            Assert.AreEqual(2, _world.All<UnitTag>().Length);
            Assert.AreEqual(new Population { Used = 2, Cap = 6 }, _world.Get<Population>(_world.Player(1)));
        }

        [Test]
        public void RallyPoint_GivesNewUnitAMoveOrder()
        {
            var prefab = UnitPrefab(0, buildTime: 0.5f);
            var producer = Producer(prefab);
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            _world.EntityManager.SetComponentEnabled<Selected>(producer, true);
            var rally = new float3(10f, 0f, 10f);

            _world.Command(1, new PlayerCommand { Type = CommandType.SetRallyPoint, Position = rally });
            _world.Produce(1, producer, prefab);
            for (var frame = 0; frame < 60 && _world.All<UnitTag>().Length == 0; frame++)
            {
                _world.Tick();
            }

            Assert.IsTrue(_world.IsEnabled<RallyPoint>(producer));
            var unit = _world.All<UnitTag>()[0];
            Assert.IsTrue(_world.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(OrderType.Move, _world.Get<ActiveOrder>(unit).Value.Type);
            Assert.AreEqual(rally, _world.Get<ActiveOrder>(unit).Value.Position);
        }

        [Test]
        public void Prerequisites_RequireACompletedBuildingOfThatType()
        {
            var prefab = UnitPrefab(10);
            _world.EntityManager.GetBuffer<Prerequisite>(prefab)
                .Add(new Prerequisite { TypeId = EntityInfo.TypeIdFromName("Barracks") });
            var producer = Producer(prefab);
            _world.Give(1, _supplies, 100);

            _world.Produce(1, producer, prefab);
            _world.Tick();
            Assert.AreEqual(0, QueueLength(producer), "no barracks");

            var barracks = _world.SpawnBuilding(1, new float3(20f, 0f, 0f), new float2(4f, 4f), complete: false,
                name: "Barracks");
            _world.Produce(1, producer, prefab);
            _world.Tick();
            Assert.AreEqual(0, QueueLength(producer), "an unfinished barracks doesn't count");

            _world.EntityManager.SetComponentEnabled<ConstructionProgress>(barracks, false);
            _world.Produce(1, producer, prefab);
            _world.Tick();
            Assert.AreEqual(1, QueueLength(producer));
            Assert.AreEqual(90, _world.Stock(1, _supplies));
        }

        [Test]
        public void Produce_RejectedByUnfinishedProducer()
        {
            var prefab = UnitPrefab(10);
            var producer = Producer(prefab, complete: false);
            _world.Give(1, _supplies, 100);

            _world.Produce(1, producer, prefab);
            _world.Tick();

            Assert.AreEqual(0, QueueLength(producer));
            Assert.AreEqual(100, _world.Stock(1, _supplies));
        }
    }
}
