using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Buildings
{
    /// <summary>Power, selling, repair and capture.</summary>
    public class BuildingRulesTests : SimulationFixture
    {
        private static readonly float2 Footprint = new(4f, 4f);

        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
        }

        [Test]
        public void LowPower_SilencesConsumers_UntilSupplyRecovers()
        {
            var plant = World.SpawnBuilding(1, new float3(-20f, 0f, 0f), Footprint, power: 5f);
            var radar = World.SpawnBuilding(1, new float3(20f, 0f, 0f), Footprint, power: -10f);
            var tower = World.SpawnBuilding(1, new float3(0f, 0f, 20f), Footprint, power: -1f);
            World.AddWeapon(tower, range: 6f, damage: 10f);
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 25f), speed: 0f);

            World.Run(1f);

            var grid = World.Get<PowerGrid>(World.Player(1));
            Assert.AreEqual(5f, grid.Produced);
            Assert.AreEqual(11f, grid.Consumed);
            Assert.AreEqual(OfflineReason.Unpowered, World.Get<Offline>(radar).Reasons);
            Assert.AreEqual(100f, World.Get<Health>(enemy).Current, "an unpowered tower holds fire");

            World.SpawnBuilding(1, new float3(-30f, 0f, 0f), Footprint, power: 10f);
            World.Run(2f);

            Assert.AreEqual(OfflineReason.None, World.Get<Offline>(radar).Reasons);
            Assert.Less(World.Get<Health>(enemy).Current, 100f, "power restored, the tower fires");
            Assert.IsTrue(World.EntityManager.Exists(plant));
        }

        [Test]
        public void LowPower_SlowsPowerConsumingProducers()
        {
            var prefab = World.MakePrefab(World.SpawnUnit(0, new float3(90f, 0f, 90f)));
            World.SetBuildTime(prefab, 2f);
            World.SpawnProvider(1, new float3(-20f, 0f, 0f), 10);
            var factory = World.MakeProducer(World.SpawnBuilding(1, float3.zero, Footprint, power: -5f),
                new float3(0f, 0f, -4f), prefab);
            World.Produce(1, factory, prefab);

            World.Run(2.5f);
            Assert.AreEqual(1, World.Buffer<ProductionQueueItem>(factory).Length, "half speed");

            World.Run(2f);
            Assert.AreEqual(0, World.Buffer<ProductionQueueItem>(factory).Length);
        }

        [Test]
        public void GameOfflineReason_SilencesTowersAndPlants_UntilCleared()
        {
            var plant = World.SpawnBuilding(1, new float3(-20f, 0f, 0f), Footprint, power: 5f);
            var tower = World.SpawnBuilding(1, new float3(0f, 0f, 20f), Footprint);
            World.AddWeapon(tower, range: 6f, damage: 10f);
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 25f), speed: 0f);
            var emp = new Offline { Reasons = OfflineReason.Custom };
            World.Set(tower, emp);
            World.Set(plant, emp);

            World.Run(1f);

            Assert.IsTrue(World.IsEnabled<Offline>(tower));
            Assert.AreEqual(100f, World.Get<Health>(enemy).Current, "an offline tower holds fire");
            Assert.AreEqual(0f, World.Get<PowerGrid>(World.Player(1)).Produced, "an offline plant supplies nothing");

            World.Set(tower, new Offline());
            World.Run(1f);

            Assert.IsFalse(World.IsEnabled<Offline>(tower));
            Assert.Less(World.Get<Health>(enemy).Current, 100f);
        }

        [Test]
        public void GameOfflineReason_HaltsProduction()
        {
            var prefab = World.MakePrefab(World.SpawnUnit(0, new float3(90f, 0f, 90f)));
            World.SetBuildTime(prefab, 1f);
            World.SpawnProvider(1, new float3(-20f, 0f, 0f), 10);
            var factory = World.MakeProducer(World.SpawnBuilding(1, float3.zero, Footprint), new float3(0f, 0f, -4f),
                prefab);
            World.Set(factory, new Offline { Reasons = OfflineReason.Custom });
            World.Produce(1, factory, prefab);

            World.Run(2f);

            Assert.AreEqual(1, World.Buffer<ProductionQueueItem>(factory).Length);
        }

        [Test]
        public void Site_IsOfflineUntilBuilt()
        {
            var site = World.SpawnBuilding(1, float3.zero, Footprint, complete: false);
            World.Tick();
            Assert.IsTrue(World.IsEnabled<Offline>(site));

            World.SetEnabled<ConstructionProgress>(site, false);
            World.Tick();

            Assert.IsFalse(World.IsEnabled<Offline>(site));
            Assert.AreEqual(OfflineReason.None, World.Get<Offline>(site).Reasons);
        }

        [Test]
        public void GameOverwritingOffline_KeepsTheEngineReasons()
        {
            var site = World.SpawnBuilding(1, float3.zero, Footprint, complete: false);
            var radar = World.SpawnBuilding(1, new float3(20f, 0f, 0f), Footprint, power: -5f);
            World.SpawnBuilding(1, new float3(-20f, 0f, 0f), Footprint, power: -5f);
            var emp = new Offline { Reasons = OfflineReason.Custom };
            World.Set(site, emp);
            World.Set(radar, emp);

            World.Tick();

            Assert.AreEqual(OfflineReason.Custom | OfflineReason.UnderConstruction, World.Get<Offline>(site).Reasons);
            Assert.AreEqual(OfflineReason.Custom | OfflineReason.Unpowered, World.Get<Offline>(radar).Reasons);
        }

        [Test]
        public void Sell_RefundsShareOfCost_AndRemovesBuilding()
        {
            var depot = World.SpawnBuilding(1, float3.zero, Footprint);
            World.SetCost(depot, _supplies, 101);
            var site = World.SpawnBuilding(1, new float3(20f, 0f, 0f), Footprint, complete: false);
            World.SetCost(site, _supplies, 40);
            var enemy = World.SpawnBuilding(2, new float3(-20f, 0f, 0f), Footprint);
            World.SetCost(enemy, _supplies, 1000);

            World.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = depot });
            World.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = depot });
            World.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = site });
            World.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = enemy });
            World.Tick();

            Assert.AreEqual(50 + 40, World.GetResources(1, _supplies),
                "half of a finished building, all of a site, once");
            Assert.IsFalse(World.EntityManager.Exists(depot));
            Assert.IsFalse(World.EntityManager.Exists(site));
            Assert.IsTrue(World.EntityManager.Exists(enemy), "only owned buildings sell");
        }

        [Test]
        public void SmartClickOnDamagedBuilding_BuildersRepairIt()
        {
            var depot = World.SpawnBuilding(1, float3.zero, Footprint, buildTime: 10f);
            World.Set(depot, new Health { Current = 100f, Max = 500f });
            var builder = World.MakeBuilder(World.SpawnUnit(1, new float3(8f, 0f, 0f)));

            World.Command(1, new PlayerCommand { Type = CommandType.Smart, Unit = builder, Target = depot });
            World.Run(4f);
            var partial = World.Get<Health>(depot).Current;
            Assert.Greater(partial, 100f);
            Assert.Less(partial, 500f);

            World.RunUntil(() => !World.IsEnabled<ActiveOrder>(builder), 10f);
            Assert.AreEqual(500f, World.Get<Health>(depot).Current);
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(builder), "the order ends when whole");
        }

        [Test]
        public void Capture_TakesOverNeutralBuilding_AndConsumesSingleUseCapturer()
        {
            var derrick = World.SpawnBuilding(0, new float3(10f, 0f, 0f), Footprint);
            var writer = World.Writer(derrick);
            CaptureSetup.AddCapturable(writer, 2f);
            var engineer = World.SpawnUnit(1, float3.zero);
            writer = World.Writer(engineer);
            CaptureSetup.AddCapturer(writer, 1f, consumedOnCapture: true);

            World.Command(1, new PlayerCommand { Type = CommandType.Smart, Unit = engineer, Target = derrick });
            World.Run(1.5f);
            Assert.AreEqual(0, World.Get<Faction>(derrick).Value, "still capturing");

            World.Run(3f);
            Assert.AreEqual(1, World.Get<Faction>(derrick).Value);
            Assert.IsFalse(World.EntityManager.Exists(engineer));
        }

        [Test]
        public void Capture_ResetsWhenAnotherPlayerStarts()
        {
            var post = World.SpawnBuilding(0, new float3(10f, 0f, 0f), Footprint);
            var writer = World.Writer(post);
            CaptureSetup.AddCapturable(writer, 100f);
            World.Set(post, new CaptureProgress { Faction = 2, Value = 0.9f });
            var ranger = World.SpawnUnit(1, new float3(5f, 0f, 0f));
            writer = World.Writer(ranger);
            CaptureSetup.AddCapturer(writer, 1f, consumedOnCapture: false);

            World.Command(1, new PlayerCommand { Type = CommandType.Capture, Unit = ranger, Target = post });
            World.Run(2f);

            var progress = World.Get<CaptureProgress>(post);
            Assert.AreEqual(1, progress.Faction);
            Assert.Less(progress.Value, 0.1f);
        }

        [Test]
        public void Capture_RefundsQueueToOldOwner_AndDropsSelectionAndRallyPoint()
        {
            var prefab = World.MakePrefab(World.SpawnUnit(0, new float3(90f, 0f, 90f)));
            World.SetBuildTime(prefab, 100f);
            World.SetCost(prefab, _supplies, 30);
            var factory = World.MakeProducer(World.SpawnBuilding(2, new float3(10f, 0f, 0f), Footprint),
                new float3(0f, 0f, -4f), prefab);
            var writer = World.Writer(factory);
            CaptureSetup.AddCapturable(writer, 1f);
            World.Buffer<ProductionQueueItem>(factory).Add(new ProductionQueueItem { Prefab = prefab });
            World.SetEnabled<Selected>(factory, true);
            World.SetEnabled<RallyPoint>(factory, true);
            var engineer = Capturer(float3.zero);
            var before = World.GetResources(2, _supplies);

            World.Command(1, new PlayerCommand { Type = CommandType.Capture, Unit = engineer, Target = factory });
            World.Run(4f);

            Assert.AreEqual(1, World.Get<Faction>(factory).Value);
            Assert.AreEqual(before + 30, World.GetResources(2, _supplies), "the old owner gets its queue back");
            Assert.AreEqual(0, World.Buffer<ProductionQueueItem>(factory).Length);
            Assert.IsFalse(World.IsEnabled<Selected>(factory));
            Assert.IsFalse(World.IsEnabled<RallyPoint>(factory), "the old owner's rally point is dropped");
        }

        [Test]
        public void Capture_IsRefused_WhileGarrisoned()
        {
            var bunker = World.SpawnBuilding(2, new float3(10f, 0f, 0f), Footprint);
            var writer = World.Writer(bunker);
            CaptureSetup.AddCapturable(writer, 1f);
            TransportSetup.AddContainer(writer, new Container { Capacity = 2, MaxPassengerSize = 1 });
            var guard = World.SpawnUnit(2, new float3(14f, 0f, 0f));
            writer = World.Writer(guard);
            TransportSetup.AddPassenger(writer, 1);
            World.Command(2, new PlayerCommand { Type = CommandType.Enter, Unit = guard, Target = bunker });
            World.Run(1f);
            Assert.IsTrue(World.IsEnabled<Inside>(guard));

            var engineer = Capturer(float3.zero);
            World.Command(1, new PlayerCommand { Type = CommandType.Capture, Unit = engineer, Target = bunker });
            World.Run(3f);

            Assert.AreEqual(2, World.Get<Faction>(bunker).Value, "the garrison has to be cleared first");
        }

        private Entity Capturer(float3 position)
        {
            var unit = World.SpawnUnit(1, position);
            var writer = World.Writer(unit);
            CaptureSetup.AddCapturer(writer, 1f, consumedOnCapture: false);
            return unit;
        }
    }
}
