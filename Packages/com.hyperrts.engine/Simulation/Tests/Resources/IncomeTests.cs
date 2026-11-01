using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Resources
{
    /// <summary>
    /// Harvesting (the loop, depletion and regrowth) and passive income: offline pauses, occupants,
    /// finite reserves with refills and casualties.
    /// </summary>
    public class IncomeTests : SimulationFixture
    {
        protected override byte[] Teams => new byte[] { 1, 2, 1 };

        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
        }

        private Entity SpawnBuilding(byte faction, float x) =>
            World.SpawnBuilding(faction, new float3(x, 0f, 0f), new float2(4f, 4f));

        private Entity SpawnDropOff(float3 position)
        {
            var building = World.SpawnBuilding(1, position, new float2(4f, 4f));
            World.EntityManager.AddComponent<ResourceDropOff>(building);
            return building;
        }

        private Entity SpawnHarvester(float3 position, float replacementRange = 30f) =>
            World.MakeHarvester(World.SpawnUnit(1, position), 10, 10f, replacementRange);

        private Entity SpawnReserve(byte faction, float x, int capacity, float interval = 1f)
        {
            var derrick = World.AddIncome(SpawnBuilding(faction, x), _supplies, 10, interval);
            var writer = World.Writer(derrick);
            writer.Add(new ResourceReserve { Remaining = capacity, Capacity = capacity });
            return derrick;
        }

        private Entity SpawnRefill(byte faction, float x, int amount)
        {
            var depot = SpawnBuilding(faction, x);
            var writer = World.Writer(depot);
            writer.Add(new ReserveRefill { Type = _supplies, Amount = amount, Interval = 1f });
            return depot;
        }

        private void SpawnCasualtyIncome(float x)
        {
            var writer = World.Writer(SpawnBuilding(1, x));
            writer.Add(new CasualtyIncome { Type = _supplies, Share = 0.5f });
        }

        private void Kill(byte faction, int cost)
        {
            var unit = World.SpawnUnit(faction, new float3(0f, 0f, 20f));
            World.SetCost(unit, _supplies, cost);
            World.Kill(unit);
        }

        [Test]
        public void Harvester_LoopsBetweenNodeAndDropOff()
        {
            SpawnDropOff(new float3(-10f, 0f, 0f));
            var node = World.SpawnNode(new float3(10f, 0f, 0f), _supplies, 100);
            var harvester = SpawnHarvester(float3.zero);

            World.Order(harvester, OrderType.Gather, node);
            World.Run(20f);

            var stock = World.GetResources(1, _supplies);
            var left = World.Get<ResourceNode>(node).Amount;
            var cargo = World.Get<Harvester>(harvester).CargoAmount;
            Assert.GreaterOrEqual(stock, 20, "several trips were delivered");
            Assert.AreEqual(100, stock + left + cargo, "nothing is created or lost");
            Assert.AreEqual(stock, World.Get<PlayerStats>(World.Player(1)).ResourcesGathered);
            Assert.IsTrue(World.IsEnabled<ActiveOrder>(harvester), "the loop keeps running");
        }

        [Test]
        public void DepletedNode_IsDestroyed_AndHarvesterDeliversThenStops()
        {
            SpawnDropOff(new float3(-6f, 0f, 0f));
            var node = World.SpawnNode(new float3(4f, 0f, 0f), _supplies, 5);
            var harvester = SpawnHarvester(float3.zero);

            World.Order(harvester, OrderType.Gather, node);
            World.RunUntil(() => World.GetResources(1, _supplies) == 5 && !World.IsEnabled<ActiveOrder>(harvester),
                10f);

            Assert.IsFalse(World.EntityManager.Exists(node));
            Assert.AreEqual(5, World.GetResources(1, _supplies));
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(harvester), "no node left, so the order completes");
        }

        [TestCase(30f, true, TestName = "DepletedNode_HarvesterMovesToNearbyNodeOfSameType")]
        [TestCase(2f, false, TestName = "DepletedNode_HarvesterIgnoresNodesBeyondItsReplacementRange")]
        public void DepletedNode_ReplacedWithinRange(float replacementRange, bool replaced)
        {
            SpawnDropOff(new float3(-6f, 0f, 0f));
            var first = World.SpawnNode(new float3(4f, 0f, 0f), _supplies, 5);
            var second = World.SpawnNode(new float3(8f, 0f, 4f), _supplies, 100);
            var harvester = SpawnHarvester(float3.zero, replacementRange);

            World.Order(harvester, OrderType.Gather, first);
            World.RunUntil(() => World.Get<ResourceNode>(second).Amount < 100 || !World.IsEnabled<ActiveOrder>(harvester),
                10f);

            Assert.IsFalse(World.EntityManager.Exists(first));
            Assert.AreEqual(replaced, World.Get<ResourceNode>(second).Amount < 100);
            if (replaced)
            {
                Assert.AreEqual(second, World.Get<ActiveOrder>(harvester).Value.Target);
            }
        }

        [Test]
        public void RenewableNode_SurvivesDepletionAndRegrows()
        {
            var node = World.SpawnNode(new float3(2f, 0f, 0f), _supplies, 5, regrowth: 10f);
            var harvester = SpawnHarvester(float3.zero);

            World.Order(harvester, OrderType.Gather, node);
            World.Run(1f);
            World.SetEnabled<ActiveOrder>(harvester, false);
            World.Run(1f);

            Assert.IsTrue(World.EntityManager.Exists(node));
            Assert.AreEqual(5, World.Get<ResourceNode>(node).Amount, "regrown to its maximum");
        }

        [Test]
        public void Income_PausesWhileOffline_AndEntitiesWithoutOfflineKeepPaying()
        {
            var derrick = World.AddIncome(SpawnBuilding(1, 0f), _supplies, 10, 1f);
            World.AddIncome(World.SpawnUnit(1, new float3(10f, 0f, 0f)), _supplies, 1, 1f);
            World.SetOffline(derrick, true);

            World.Run(2.5f);
            Assert.AreEqual(2, World.GetResources(1, _supplies), "only the entity without Offline paid");

            World.SetOffline(derrick, false);
            World.Run(1f);
            Assert.AreEqual(13, World.GetResources(1, _supplies), "back online, the paused timer resumes");
            Assert.AreEqual(13, World.Get<PlayerStats>(World.Player(1)).ResourcesGathered);
        }

        [Test]
        public void OccupantIncome_PaysPerPassenger_AndNothingWhenEmpty()
        {
            var bunker = World.AddIncome(SpawnBuilding(1, 0f), _supplies, 5, 1f);
            var writer = World.Writer(bunker);
            TransportSetup.AddContainer(writer, new Container { Capacity = 4, MaxPassengerSize = 1 });
            writer.Add<OccupantIncome>();

            World.Run(1.5f);
            Assert.AreEqual(0, World.GetResources(1, _supplies), "empty, it pays nothing");

            for (var i = 0; i < 3; i++)
            {
                var unit = World.SpawnUnit(1, new float3(10f + i, 0f, 0f));
                World.Buffer<Cargo>(bunker).Add(new Cargo { Unit = unit, Size = 1 });
            }

            World.Run(1f);
            Assert.AreEqual(15, World.GetResources(1, _supplies), "one payment for each of three passengers");
        }

        [Test]
        public void Reserve_RunsDry_ThenARefillTopsItUp()
        {
            var derrick = SpawnReserve(1, 0f, 25);

            World.Run(4.5f);
            Assert.AreEqual(25, World.GetResources(1, _supplies), "never more than the reserve held");
            Assert.AreEqual(0, World.Get<ResourceReserve>(derrick).Remaining);
            Assert.IsTrue(World.EntityManager.Exists(derrick), "a dry reserve stays to be refilled");

            SpawnRefill(1, 10f, 7);
            World.Run(2f);
            Assert.AreEqual(32, World.GetResources(1, _supplies), "the refill was paid out again");
        }

        [Test]
        public void Refill_CapsAtCapacity_SkipsOtherOwners_AndPausesWhileOffline()
        {
            var own = SpawnReserve(1, 0f, 20, interval: 1000f);
            var enemy = SpawnReserve(2, 30f, 20, interval: 1000f);
            World.Set(own, new ResourceReserve { Remaining = 15, Capacity = 20 });
            World.Set(enemy, new ResourceReserve { Remaining = 0, Capacity = 20 });
            var depot = SpawnRefill(1, 10f, 10);

            World.Run(1.5f);
            Assert.AreEqual(20, World.Get<ResourceReserve>(own).Remaining, "capped at capacity");
            Assert.AreEqual(0, World.Get<ResourceReserve>(enemy).Remaining, "another owner's reserve is untouched");

            World.Set(own, new ResourceReserve { Remaining = 0, Capacity = 20 });
            World.SetOffline(depot, true);
            World.Run(2f);
            Assert.AreEqual(0, World.Get<ResourceReserve>(own).Remaining, "an offline refill adds nothing");
        }

        [Test]
        public void CasualtyIncome_PaysShareOfOwnAndHostileDeaths_PerEarner_AndNothingWhileOffline()
        {
            SpawnCasualtyIncome(0f);
            SpawnCasualtyIncome(10f);

            Kill(1, 100);
            Kill(2, 40);
            Kill(3, 1000);
            World.Tick();

            Assert.AreEqual(140, World.GetResources(1, _supplies), "half of own and enemy costs, twice; allies pay nothing");
            Assert.AreEqual(140, World.Get<PlayerStats>(World.Player(1)).ResourcesGathered);

            foreach (var earner in World.All<CasualtyIncome>().ToArray())
            {
                World.SetOffline(earner, true);
            }

            World.Tick();
            Kill(2, 100);
            World.Tick();
            Assert.AreEqual(140, World.GetResources(1, _supplies), "offline earners pay nothing");
        }
    }
}
