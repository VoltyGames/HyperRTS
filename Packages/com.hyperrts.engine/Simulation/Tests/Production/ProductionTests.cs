using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Production
{
    /// <summary>Building placement, construction, production queues, rally points and prerequisites.</summary>
    public class ProductionTests : SimulationFixture
    {
        private static readonly float3 Far = new(90f, 0f, 90f);

        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
        }

        private Entity BuildingPrefab(int cost)
        {
            var prefab = World.MakePrefab(World.SpawnBuilding(0, Far, new float2(4f, 4f), new TestBuilding
            {
                BuildTime = 2f,
                Name = "Barracks",
            }));
            World.SetCost(prefab, _supplies, cost);
            return prefab;
        }

        [Test]
        public void PlaceBuilding_SpendsCost_AndBuilderFinishesConstruction()
        {
            var prefab = BuildingPrefab(50);
            var builder = World.MakeBuilder(World.SpawnUnit(1, float3.zero), prefab);
            World.AddResources(1, _supplies, 100);

            World.PlaceBuilding(builder, prefab, new float3(6.3f, 0f, 0.2f));
            World.Tick();

            Assert.AreEqual(50, World.GetResources(1, _supplies));
            var sites = World.All<BuildingTag>();
            Assert.AreEqual(1, sites.Length);
            var site = sites[0];
            Assert.AreEqual(new float3(6f, 0f, 0f), World.Get<LocalTransform>(site).Position, "snapped to the grid");
            Assert.AreEqual(1, World.Get<Owner>(site).Value);
            Assert.IsTrue(World.IsEnabled<ConstructionProgress>(site));
            Assert.AreEqual(OrderType.Build, World.Get<ActiveOrder>(builder).Value.Type);

            World.Run(4f);

            Assert.IsFalse(World.IsEnabled<ConstructionProgress>(site));
            Assert.AreEqual(1f, World.Get<ConstructionProgress>(site).Value);
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(builder), "the Build order completes with the site");
            Assert.AreEqual(1, World.Get<PlayerStats>(World.Player(1)).BuildingsBuilt);
        }

        [Test]
        public void SiteSetToFullProgress_CompletesLikeABuiltOne_AndRaisesBuildingCompletedForOneFrame()
        {
            var site = World.SpawnBuilding(1, float3.zero, new float2(4f, 4f), new TestBuilding { Complete = false });
            World.Set(site, new ConstructionProgress { Value = 1f });

            World.Tick();

            Assert.IsFalse(World.IsEnabled<ConstructionProgress>(site));
            Assert.AreEqual(1, Events<BuildingCompleted>().Length);
            Assert.AreEqual(site, Events<BuildingCompleted>()[0].Building);
            Assert.AreEqual(1, World.Get<PlayerStats>(World.Player(1)).BuildingsBuilt);
            World.Tick();
            Assert.AreEqual(0, Events<BuildingCompleted>().Length, "cleared at the start of the next frame");
        }

        private DynamicBuffer<T> Events<T>() where T : unmanaged, IBufferElementData
        {
            var events = World.Query(typeof(GameEvents)).GetSingletonEntity();
            return World.Buffer<T>(events);
        }

        [Test]
        public void PlaceBuilding_RejectedOnOccupiedGround()
        {
            var prefab = BuildingPrefab(50);
            var builder = World.MakeBuilder(World.SpawnUnit(1, float3.zero), prefab);
            World.SpawnBuilding(1, new float3(6f, 0f, 0f), new float2(4f, 4f));
            World.AddResources(1, _supplies, 100);

            World.PlaceBuilding(builder, prefab, new float3(7f, 0f, 1f));
            World.Tick();

            Assert.AreEqual(100, World.GetResources(1, _supplies));
            Assert.AreEqual(1, World.All<BuildingTag>().Length);
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(builder));
        }

        [Test]
        public void PlaceBuilding_RejectedWithoutMoney()
        {
            var prefab = BuildingPrefab(50);
            var builder = World.MakeBuilder(World.SpawnUnit(1, float3.zero), prefab);
            World.AddResources(1, _supplies, 40);

            World.PlaceBuilding(builder, prefab, new float3(6f, 0f, 0f));
            World.Tick();

            Assert.AreEqual(40, World.GetResources(1, _supplies));
            Assert.AreEqual(0, World.All<BuildingTag>().Length);
        }

        [Test]
        public void Produce_SpendsCost_AndSpawnsUnitAfterBuildTime()
        {
            var prefab = World.UnitPrefab(_supplies, 30);
            var producer = World.SpawnProducer(prefab);
            World.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            World.AddResources(1, _supplies, 100);

            World.Produce(1, producer, prefab);
            World.Tick();
            Assert.AreEqual(70, World.GetResources(1, _supplies));
            Assert.AreEqual(1, World.QueueLength(producer));

            World.Run(0.5f);
            Assert.AreEqual(0, World.All<UnitTag>().Length, "still training");

            World.Run(1f);
            var units = World.All<UnitTag>();
            Assert.AreEqual(1, units.Length);
            Assert.AreEqual(new float3(0f, 0f, -4f), World.Get<LocalTransform>(units[0]).Position);
            Assert.AreEqual(1, World.Get<Owner>(units[0]).Value);
            Assert.AreEqual(0, World.QueueLength(producer));
            Assert.AreEqual(1, World.Get<PlayerStats>(World.Player(1)).UnitsBuilt);
            Assert.AreEqual(1u, World.Get<Producer>(producer).Released, "every client sees the release");
        }

        [Test]
        public void CancelProduction_RefundsCost()
        {
            var prefab = World.UnitPrefab(_supplies, 30);
            var producer = World.SpawnProducer(prefab);
            World.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            World.AddResources(1, _supplies, 100);

            World.Produce(1, producer, prefab);
            World.Produce(1, producer, prefab);
            World.Tick();
            Assert.AreEqual(40, World.GetResources(1, _supplies));

            World.Command(1, new PlayerCommand { Type = CommandType.CancelProduction, Unit = producer, Argument = -1 });
            World.Tick();

            Assert.AreEqual(70, World.GetResources(1, _supplies));
            Assert.AreEqual(1, World.QueueLength(producer));
        }

        [Test]
        public void PopulationCap_BlocksProductionUntilRaised()
        {
            var prefab = World.UnitPrefab(_supplies, 10);
            var producer = World.SpawnProducer(prefab);
            World.SpawnProvider(1, new float3(20f, 0f, 20f), 1);
            World.SpawnUnit(1, new float3(10f, 0f, 10f));
            World.AddResources(1, _supplies, 100);

            World.Produce(1, producer, prefab);
            World.Run(3f);
            Assert.AreEqual(1, World.All<UnitTag>().Length, "the cap is full");
            Assert.AreEqual(1, World.QueueLength(producer));

            World.SpawnProvider(1, new float3(-20f, 0f, 20f), 5);
            World.Run(1.5f);
            Assert.AreEqual(2, World.All<UnitTag>().Length);
            Assert.AreEqual(new Population { Used = 2, Cap = 6 }, World.Get<Population>(World.Player(1)));
        }

        [Test]
        public void RallyPoint_SendsTheNewUnitThere()
        {
            var prefab = World.UnitPrefab(_supplies, 0, buildTime: 0.5f);
            var producer = World.SpawnProducer(prefab);
            World.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            World.SetEnabled<Selected>(producer, true);
            var rally = new float3(10f, 0f, 10f);

            World.Command(1, new PlayerCommand { Type = CommandType.SetRallyPoint, Position = rally });
            World.Produce(1, producer, prefab);
            for (var frame = 0; frame < 60 && World.All<UnitTag>().Length == 0; frame++)
            {
                World.Tick();
            }

            // RallyOrderSystem gives the move the frame after the spawn.
            World.Tick();
            Assert.IsTrue(World.IsEnabled<RallyPoint>(producer));
            var unit = World.All<UnitTag>()[0];
            Assert.IsTrue(World.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(OrderType.Move, World.Get<ActiveOrder>(unit).Value.Type);
            Assert.AreEqual(rally, World.Get<ActiveOrder>(unit).Value.Position);

            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<ActiveOrder>(unit), 10f), "the move finished");
            Assert.Less(math.distance(World.GetPosition(unit).xz, rally.xz), 0.5f, "drove to the rally point");
        }

        [Test]
        public void Prerequisites_RequireACompletedBuildingOfThatType()
        {
            var prefab = World.UnitPrefab(_supplies, 10);
            World.Buffer<Prerequisite>(prefab)
                .Add(new Prerequisite { TypeId = EntityInfo.TypeIdFromName("Barracks") });
            var producer = World.SpawnProducer(prefab);
            World.AddResources(1, _supplies, 100);

            World.Produce(1, producer, prefab);
            World.Tick();
            Assert.AreEqual(0, World.QueueLength(producer), "no barracks");

            var barracks = World.SpawnBuilding(1, new float3(20f, 0f, 0f), new float2(4f, 4f), new TestBuilding
            {
                Complete = false,
                Name = "Barracks",
            });
            World.Produce(1, producer, prefab);
            World.Tick();
            Assert.AreEqual(0, World.QueueLength(producer), "an unfinished barracks doesn't count");

            World.SetEnabled<ConstructionProgress>(barracks, false);
            World.Produce(1, producer, prefab);
            World.Tick();
            Assert.AreEqual(1, World.QueueLength(producer));
            Assert.AreEqual(90, World.GetResources(1, _supplies));
        }

        [Test]
        public void Produce_RejectedByUnfinishedProducer()
        {
            var prefab = World.UnitPrefab(_supplies, 10);
            var producer = World.SpawnProducer(prefab, complete: false);
            World.AddResources(1, _supplies, 100);

            World.Produce(1, producer, prefab);
            World.Tick();

            Assert.AreEqual(0, World.QueueLength(producer));
            Assert.AreEqual(100, World.GetResources(1, _supplies));
        }
    }
}
