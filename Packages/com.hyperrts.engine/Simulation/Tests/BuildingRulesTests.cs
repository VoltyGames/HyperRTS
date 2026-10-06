using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Resources;
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
            _world.Arm(tower, range: 6f, damage: 10f);
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

            Assert.AreEqual(50 + 40, _world.Stock(1, _supplies), "half of a finished building, all of a site, once");
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
            var sink = new EntityManagerSink(_world.EntityManager, derrick);
            CaptureSetup.AddCapturable(ref sink, 2f);
            var engineer = _world.SpawnUnit(1, float3.zero);
            sink = new EntityManagerSink(_world.EntityManager, engineer);
            CaptureSetup.AddCapturer(ref sink, 1f, consumedOnCapture: true);

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
            var sink = new EntityManagerSink(_world.EntityManager, post);
            CaptureSetup.AddCapturable(ref sink, 100f);
            _world.EntityManager.SetComponentData(post, new CaptureProgress { Faction = 2, Value = 0.9f });
            var ranger = _world.SpawnUnit(1, new float3(5f, 0f, 0f));
            sink = new EntityManagerSink(_world.EntityManager, ranger);
            CaptureSetup.AddCapturer(ref sink, 1f, consumedOnCapture: false);

            _world.Command(1, new PlayerCommand { Type = CommandType.Capture, Unit = ranger, Target = post });
            _world.Run(2f);

            var progress = _world.Get<CaptureProgress>(post);
            Assert.AreEqual(1, progress.Faction);
            Assert.Less(progress.Value, 0.1f);
        }
    }
}
