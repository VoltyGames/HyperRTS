using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Economy
{
    /// <summary>
    /// Harvesting (the loop, depletion and regrowth) and passive income: offline pauses, occupants,
    /// finite reserves with refills and casualties.
    /// </summary>
    public class IncomeTests
    {
        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2, 1);
            _supplies = _world.CreateSupplies();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity SpawnBuilding(byte faction, float x) =>
            _world.SpawnBuilding(faction, new float3(x, 0f, 0f), new float2(4f, 4f));

        private Entity SpawnDropOff(float3 position)
        {
            var building = _world.SpawnBuilding(1, position, new float2(4f, 4f));
            _world.EntityManager.AddComponent<ResourceDropOff>(building);
            return building;
        }

        private Entity SpawnHarvester(float3 position, float replacementRange = 30f) =>
            _world.MakeHarvester(_world.SpawnUnit(1, position), 10, 10f, replacementRange);

        private Entity SpawnReserve(byte faction, float x, int capacity, float interval = 1f)
        {
            var derrick = _world.AddIncome(SpawnBuilding(faction, x), _supplies, 10, interval);
            var writer = new EntityManagerWriter(_world.EntityManager, derrick);
            writer.Add(new ResourceReserve { Remaining = capacity, Capacity = capacity });
            return derrick;
        }

        private Entity SpawnRefill(byte faction, float x, int amount)
        {
            var depot = SpawnBuilding(faction, x);
            var writer = new EntityManagerWriter(_world.EntityManager, depot);
            writer.Add(new ReserveRefill { Type = _supplies, Amount = amount, Interval = 1f });
            return depot;
        }

        private void SpawnCasualtyIncome(float x)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, SpawnBuilding(1, x));
            writer.Add(new CasualtyIncome { Type = _supplies, Share = 0.5f });
        }

        private void Kill(byte faction, int cost)
        {
            var unit = _world.SpawnUnit(faction, new float3(0f, 0f, 20f));
            _world.SetCost(unit, _supplies, cost);
            _world.Kill(unit);
        }

        [Test]
        public void Harvester_LoopsBetweenNodeAndDropOff()
        {
            SpawnDropOff(new float3(-10f, 0f, 0f));
            var node = _world.SpawnNode(new float3(10f, 0f, 0f), _supplies, 100);
            var harvester = SpawnHarvester(float3.zero);

            _world.Order(harvester, OrderType.Gather, node);
            _world.Run(20f);

            var stock = _world.GetResources(1, _supplies);
            var left = _world.Get<ResourceNode>(node).Amount;
            var cargo = _world.Get<Harvester>(harvester).CargoAmount;
            Assert.GreaterOrEqual(stock, 20, "several trips were delivered");
            Assert.AreEqual(100, stock + left + cargo, "nothing is created or lost");
            Assert.AreEqual(stock, _world.Get<PlayerStats>(_world.Player(1)).ResourcesGathered);
            Assert.IsTrue(_world.IsEnabled<ActiveOrder>(harvester), "the loop keeps running");
        }

        [Test]
        public void DepletedNode_IsDestroyed_AndHarvesterDeliversThenStops()
        {
            SpawnDropOff(new float3(-6f, 0f, 0f));
            var node = _world.SpawnNode(new float3(4f, 0f, 0f), _supplies, 5);
            var harvester = SpawnHarvester(float3.zero);

            _world.Order(harvester, OrderType.Gather, node);
            _world.RunUntil(() => _world.GetResources(1, _supplies) == 5 && !_world.IsEnabled<ActiveOrder>(harvester),
                10f);

            Assert.IsFalse(_world.EntityManager.Exists(node));
            Assert.AreEqual(5, _world.GetResources(1, _supplies));
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(harvester), "no node left, so the order completes");
        }

        [TestCase(30f, true, TestName = "DepletedNode_HarvesterMovesToNearbyNodeOfSameType")]
        [TestCase(2f, false, TestName = "DepletedNode_HarvesterIgnoresNodesBeyondItsReplacementRange")]
        public void DepletedNode_ReplacedWithinRange(float replacementRange, bool replaced)
        {
            SpawnDropOff(new float3(-6f, 0f, 0f));
            var first = _world.SpawnNode(new float3(4f, 0f, 0f), _supplies, 5);
            var second = _world.SpawnNode(new float3(8f, 0f, 4f), _supplies, 100);
            var harvester = SpawnHarvester(float3.zero, replacementRange);

            _world.Order(harvester, OrderType.Gather, first);
            _world.RunUntil(() => _world.Get<ResourceNode>(second).Amount < 100 || !_world.IsEnabled<ActiveOrder>(harvester),
                10f);

            Assert.IsFalse(_world.EntityManager.Exists(first));
            Assert.AreEqual(replaced, _world.Get<ResourceNode>(second).Amount < 100);
            if (replaced)
            {
                Assert.AreEqual(second, _world.Get<ActiveOrder>(harvester).Value.Target);
            }
        }

        [Test]
        public void RenewableNode_SurvivesDepletionAndRegrows()
        {
            var node = _world.SpawnNode(new float3(2f, 0f, 0f), _supplies, 5, regrowth: 10f);
            var harvester = SpawnHarvester(float3.zero);

            _world.Order(harvester, OrderType.Gather, node);
            _world.Run(1f);
            _world.EntityManager.SetComponentEnabled<ActiveOrder>(harvester, false);
            _world.Run(1f);

            Assert.IsTrue(_world.EntityManager.Exists(node));
            Assert.AreEqual(5, _world.Get<ResourceNode>(node).Amount, "regrown to its maximum");
        }

        [Test]
        public void Income_PausesWhileOffline_AndEntitiesWithoutOfflineKeepPaying()
        {
            var derrick = _world.AddIncome(SpawnBuilding(1, 0f), _supplies, 10, 1f);
            _world.AddIncome(_world.SpawnUnit(1, new float3(10f, 0f, 0f)), _supplies, 1, 1f);
            _world.SetOffline(derrick, true);

            _world.Run(2.5f);
            Assert.AreEqual(2, _world.GetResources(1, _supplies), "only the entity without Offline paid");

            _world.SetOffline(derrick, false);
            _world.Run(1f);
            Assert.AreEqual(13, _world.GetResources(1, _supplies), "back online, the paused timer resumes");
            Assert.AreEqual(13, _world.Get<PlayerStats>(_world.Player(1)).ResourcesGathered);
        }

        [Test]
        public void OccupantIncome_PaysPerPassenger_AndNothingWhenEmpty()
        {
            var bunker = _world.AddIncome(SpawnBuilding(1, 0f), _supplies, 5, 1f);
            var writer = new EntityManagerWriter(_world.EntityManager, bunker);
            TransportSetup.AddContainer(ref writer, new Container { Capacity = 4, MaxPassengerSize = 1 });
            writer.Add<OccupantIncome>();

            _world.Run(1.5f);
            Assert.AreEqual(0, _world.GetResources(1, _supplies), "empty, it pays nothing");

            for (var i = 0; i < 3; i++)
            {
                var unit = _world.SpawnUnit(1, new float3(10f + i, 0f, 0f));
                _world.EntityManager.GetBuffer<Cargo>(bunker).Add(new Cargo { Unit = unit, Size = 1 });
            }

            _world.Run(1f);
            Assert.AreEqual(15, _world.GetResources(1, _supplies), "one payment for each of three passengers");
        }

        [Test]
        public void Reserve_RunsDry_ThenARefillTopsItUp()
        {
            var derrick = SpawnReserve(1, 0f, 25);

            _world.Run(4.5f);
            Assert.AreEqual(25, _world.GetResources(1, _supplies), "never more than the reserve held");
            Assert.AreEqual(0, _world.Get<ResourceReserve>(derrick).Remaining);
            Assert.IsTrue(_world.EntityManager.Exists(derrick), "a dry reserve stays to be refilled");

            SpawnRefill(1, 10f, 7);
            _world.Run(2f);
            Assert.AreEqual(32, _world.GetResources(1, _supplies), "the refill was paid out again");
        }

        [Test]
        public void Refill_CapsAtCapacity_SkipsOtherOwners_AndPausesWhileOffline()
        {
            var own = SpawnReserve(1, 0f, 20, interval: 1000f);
            var enemy = SpawnReserve(2, 30f, 20, interval: 1000f);
            _world.EntityManager.SetComponentData(own, new ResourceReserve { Remaining = 15, Capacity = 20 });
            _world.EntityManager.SetComponentData(enemy, new ResourceReserve { Remaining = 0, Capacity = 20 });
            var depot = SpawnRefill(1, 10f, 10);

            _world.Run(1.5f);
            Assert.AreEqual(20, _world.Get<ResourceReserve>(own).Remaining, "capped at capacity");
            Assert.AreEqual(0, _world.Get<ResourceReserve>(enemy).Remaining, "another owner's reserve is untouched");

            _world.EntityManager.SetComponentData(own, new ResourceReserve { Remaining = 0, Capacity = 20 });
            _world.SetOffline(depot, true);
            _world.Run(2f);
            Assert.AreEqual(0, _world.Get<ResourceReserve>(own).Remaining, "an offline refill adds nothing");
        }

        [Test]
        public void CasualtyIncome_PaysShareOfOwnAndHostileDeaths_PerEarner_AndNothingWhileOffline()
        {
            SpawnCasualtyIncome(0f);
            SpawnCasualtyIncome(10f);

            Kill(1, 100);
            Kill(2, 40);
            Kill(3, 1000);
            _world.Tick();

            Assert.AreEqual(140, _world.GetResources(1, _supplies), "half of own and enemy costs, twice; allies pay nothing");
            Assert.AreEqual(140, _world.Get<PlayerStats>(_world.Player(1)).ResourcesGathered);

            foreach (var earner in _world.All<CasualtyIncome>().ToArray())
            {
                _world.SetOffline(earner, true);
            }

            _world.Tick();
            Kill(2, 100);
            _world.Tick();
            Assert.AreEqual(140, _world.GetResources(1, _supplies), "offline earners pay nothing");
        }
    }
}
