using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Production
{
    /// <summary>Per-player build limits on queued, placed and standing units and buildings.</summary>
    public class BuildLimitTests : SimulationFixture
    {
        private static readonly float3 Far = new(90f, 0f, 90f);

        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
            World.AddResources(1, _supplies, 1000);
        }

        private Entity HeroPrefab(int max) =>
            World.AddBuildLimit(World.UnitPrefab(_supplies, 100, buildTime: 30f, name: "Hero"), max);

        private Entity LiveHero(byte owner, Entity prefab)
        {
            var hero = World.SpawnUnit(owner, new float3(-20f, 0f, 0f), new TestUnit { Name = "Hero" });
            return World.AddBuildLimit(hero, World.Get<BuildLimit>(prefab).Max);
        }

        [Test]
        public void Produce_StopsQueueingAtTheLimit_AndChargesOnlyWhatQueued()
        {
            var prefab = HeroPrefab(2);
            var producer = World.SpawnProducer(prefab);

            for (var i = 0; i < 3; i++)
            {
                World.Produce(1, producer, prefab);
            }

            World.Tick();

            Assert.AreEqual(2, World.QueueLength(producer));
            Assert.AreEqual(800, World.GetResources(1, _supplies));
        }

        [Test]
        public void Produce_CountsOwnStandingUnits_NotOtherPlayers()
        {
            var prefab = HeroPrefab(2);
            var producer = World.SpawnProducer(prefab);
            LiveHero(1, prefab);
            LiveHero(2, prefab);
            LiveHero(2, prefab);

            World.Produce(1, producer, prefab);
            World.Produce(1, producer, prefab);
            World.Tick();

            Assert.AreEqual(1, World.QueueLength(producer));
        }

        [Test]
        public void Produce_DeadUnitFreesItsSlot()
        {
            var prefab = HeroPrefab(1);
            var producer = World.SpawnProducer(prefab);
            var hero = LiveHero(1, prefab);
            World.SetEnabled<Dead>(hero, true);

            World.Produce(1, producer, prefab);
            World.Tick();

            Assert.AreEqual(1, World.QueueLength(producer));
        }

        [Test]
        public void PlaceBuilding_SecondSiteInTheSameFrameIsRefused()
        {
            var silo = World.SpawnBuilding(0, Far, new float2(4f, 4f), new TestBuilding { Name = "Silo" });
            var prefab = World.AddBuildLimit(World.MakePrefab(silo), 1);
            World.SetCost(prefab, _supplies, 100);
            var builder = World.MakeBuilder(World.SpawnUnit(1, float3.zero), prefab);

            World.PlaceBuilding(builder, prefab, new float3(10f, 0f, 0f));
            World.PlaceBuilding(builder, prefab, new float3(-10f, 0f, 0f));
            World.Tick();
            World.PlaceBuilding(builder, prefab, new float3(0f, 0f, 10f));
            World.Tick();

            Assert.AreEqual(1, World.All<BuildLimit>().Length);
            Assert.AreEqual(900, World.GetResources(1, _supplies));
        }
    }
}
