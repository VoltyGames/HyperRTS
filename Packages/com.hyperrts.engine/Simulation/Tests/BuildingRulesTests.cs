using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Power, selling, repair and capture.</summary>
    public class BuildingRulesTests
    {
        private static readonly float2 Footprint = new(4f, 4f);

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

        [Test]
        public void LowPower_SilencesConsumers_UntilSupplyRecovers()
        {
            var plant = _world.SpawnBuilding(1, new float3(-20f, 0f, 0f), Footprint, power: 5f);
            var radar = _world.SpawnBuilding(1, new float3(20f, 0f, 0f), Footprint, power: -10f);
            var tower = _world.SpawnBuilding(1, new float3(0f, 0f, 20f), Footprint, power: -1f);
            _world.AddWeapon(tower, range: 6f, damage: 10f);
            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 25f), speed: 0f);

            _world.Run(1f);

            var grid = _world.Get<PowerGrid>(_world.Player(1));
            Assert.AreEqual(5f, grid.Produced);
            Assert.AreEqual(11f, grid.Consumed);
            Assert.IsTrue(_world.IsEnabled<Unpowered>(radar));
            Assert.AreEqual(100f, _world.Get<Health>(enemy).Current, "an unpowered tower holds fire");

            _world.SpawnBuilding(1, new float3(-30f, 0f, 0f), Footprint, power: 10f);
            _world.Run(2f);

            Assert.IsFalse(_world.IsEnabled<Unpowered>(radar));
            Assert.Less(_world.Get<Health>(enemy).Current, 100f, "power restored, the tower fires");
            Assert.IsTrue(_world.EntityManager.Exists(plant));
        }

        [Test]
        public void LowPower_SlowsPowerConsumingProducers()
        {
            var prefab = _world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 90f)));
            _world.SetBuildTime(prefab, 2f);
            _world.SpawnProvider(1, new float3(-20f, 0f, 0f), 10);
            var factory = _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, Footprint, power: -5f),
                new float3(0f, 0f, -4f), prefab);
            _world.Produce(1, factory, prefab);

            _world.Run(2.5f);
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<ProductionQueueItem>(factory).Length, "half speed");

            _world.Run(2f);
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<ProductionQueueItem>(factory).Length);
        }

        [Test]
        public void Sell_RefundsShareOfCost_AndRemovesBuilding()
        {
            var depot = _world.SpawnBuilding(1, float3.zero, Footprint);
            _world.SetCost(depot, _supplies, 101);
            var site = _world.SpawnBuilding(1, new float3(20f, 0f, 0f), Footprint, complete: false);
            _world.SetCost(site, _supplies, 40);
            var enemy = _world.SpawnBuilding(2, new float3(-20f, 0f, 0f), Footprint);
            _world.SetCost(enemy, _supplies, 1000);

            _world.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = depot });
            _world.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = depot });
            _world.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = site });
            _world.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = enemy });
            _world.Tick();

            Assert.AreEqual(50 + 40, _world.ResourcesOf(1, _supplies),
                "half of a finished building, all of a site, once");
            Assert.IsFalse(_world.EntityManager.Exists(depot));
            Assert.IsFalse(_world.EntityManager.Exists(site));
            Assert.IsTrue(_world.EntityManager.Exists(enemy), "only owned buildings sell");
        }

        [Test]
        public void SmartClickOnDamagedBuilding_BuildersRepairIt()
        {
            var depot = _world.SpawnBuilding(1, float3.zero, Footprint, buildTime: 10f);
            _world.EntityManager.SetComponentData(depot, new Health { Current = 100f, Max = 500f });
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, new float3(8f, 0f, 0f)));

            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Unit = builder, Target = depot });
            _world.Run(4f);
            var partial = _world.Get<Health>(depot).Current;
            Assert.Greater(partial, 100f);
            Assert.Less(partial, 500f);

            _world.Run(10f);
            Assert.AreEqual(500f, _world.Get<Health>(depot).Current);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(builder), "the order ends when whole");
        }

        [Test]
        public void Capture_TakesOverNeutralBuilding_AndConsumesSingleUseCapturer()
        {
            var derrick = _world.SpawnBuilding(0, new float3(10f, 0f, 0f), Footprint);
            var writer = new EntityManagerWriter(_world.EntityManager, derrick);
            CaptureSetup.AddCapturable(ref writer, 2f);
            var engineer = _world.SpawnUnit(1, float3.zero);
            writer = new EntityManagerWriter(_world.EntityManager, engineer);
            CaptureSetup.AddCapturer(ref writer, 1f, consumedOnCapture: true);

            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Unit = engineer, Target = derrick });
            _world.Run(1.5f);
            Assert.AreEqual(0, _world.Get<Faction>(derrick).Value, "still capturing");

            _world.Run(3f);
            Assert.AreEqual(1, _world.Get<Faction>(derrick).Value);
            Assert.IsFalse(_world.EntityManager.Exists(engineer));
        }

        [Test]
        public void Capture_ResetsWhenAnotherPlayerStarts()
        {
            var post = _world.SpawnBuilding(0, new float3(10f, 0f, 0f), Footprint);
            var writer = new EntityManagerWriter(_world.EntityManager, post);
            CaptureSetup.AddCapturable(ref writer, 100f);
            _world.EntityManager.SetComponentData(post, new CaptureProgress { Faction = 2, Value = 0.9f });
            var ranger = _world.SpawnUnit(1, new float3(5f, 0f, 0f));
            writer = new EntityManagerWriter(_world.EntityManager, ranger);
            CaptureSetup.AddCapturer(ref writer, 1f, consumedOnCapture: false);

            _world.Command(1, new PlayerCommand { Type = CommandType.Capture, Unit = ranger, Target = post });
            _world.Run(2f);

            var progress = _world.Get<CaptureProgress>(post);
            Assert.AreEqual(1, progress.Faction);
            Assert.Less(progress.Value, 0.1f);
        }

        [Test]
        public void Capture_RefundsQueueToOldOwner_AndDropsSelectionAndRallyPoint()
        {
            var prefab = _world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 90f)));
            _world.SetBuildTime(prefab, 100f);
            _world.SetCost(prefab, _supplies, 30);
            var factory = _world.MakeProducer(_world.SpawnBuilding(2, new float3(10f, 0f, 0f), Footprint),
                new float3(0f, 0f, -4f), prefab);
            var writer = new EntityManagerWriter(_world.EntityManager, factory);
            CaptureSetup.AddCapturable(ref writer, 1f);
            _world.EntityManager.GetBuffer<ProductionQueueItem>(factory).Add(new ProductionQueueItem { Prefab = prefab });
            _world.EntityManager.SetComponentEnabled<Selected>(factory, true);
            _world.EntityManager.SetComponentEnabled<RallyPoint>(factory, true);
            var engineer = Capturer(float3.zero);
            var before = _world.ResourcesOf(2, _supplies);

            _world.Command(1, new PlayerCommand { Type = CommandType.Capture, Unit = engineer, Target = factory });
            _world.Run(4f);

            Assert.AreEqual(1, _world.Get<Faction>(factory).Value);
            Assert.AreEqual(before + 30, _world.ResourcesOf(2, _supplies), "the old owner gets its queue back");
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<ProductionQueueItem>(factory).Length);
            Assert.IsFalse(_world.IsEnabled<Selected>(factory));
            Assert.IsFalse(_world.IsEnabled<RallyPoint>(factory), "the old owner's rally point is dropped");
        }

        [Test]
        public void Capture_IsRefused_WhileGarrisoned()
        {
            var bunker = _world.SpawnBuilding(2, new float3(10f, 0f, 0f), Footprint);
            var writer = new EntityManagerWriter(_world.EntityManager, bunker);
            CaptureSetup.AddCapturable(ref writer, 1f);
            TransportSetup.AddContainer(ref writer, new Container { Capacity = 2, MaxPassengerSize = 1 });
            var guard = _world.SpawnUnit(2, new float3(14f, 0f, 0f));
            writer = new EntityManagerWriter(_world.EntityManager, guard);
            TransportSetup.AddPassenger(ref writer, 1);
            _world.Command(2, new PlayerCommand { Type = CommandType.Enter, Unit = guard, Target = bunker });
            _world.Run(1f);
            Assert.IsTrue(_world.IsEnabled<Inside>(guard));

            var engineer = Capturer(float3.zero);
            _world.Command(1, new PlayerCommand { Type = CommandType.Capture, Unit = engineer, Target = bunker });
            _world.Run(3f);

            Assert.AreEqual(2, _world.Get<Faction>(bunker).Value, "the garrison has to be cleared first");
        }

        private Entity Capturer(float3 position)
        {
            var unit = _world.SpawnUnit(1, position);
            var writer = new EntityManagerWriter(_world.EntityManager, unit);
            CaptureSetup.AddCapturer(ref writer, 1f, consumedOnCapture: false);
            return unit;
        }
    }
}
