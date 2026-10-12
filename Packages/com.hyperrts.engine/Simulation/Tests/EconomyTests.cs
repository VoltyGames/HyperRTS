using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Harvest loop, drop-off choice, node depletion and regrowth.</summary>
    public class EconomyTests
    {
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

        private Entity SpawnDropOff(float3 position)
        {
            var building = _world.SpawnBuilding(1, position, new float2(4f, 4f));
            var writer = new EntityManagerWriter(_world.EntityManager, building);
            ResourceDropOffSetup.Add(ref writer);
            return building;
        }

        private Entity SpawnHarvester(float3 position, int capacity = 10, float rate = 10f) =>
            _world.MakeHarvester(_world.SpawnUnit(1, position), capacity, rate);

        [Test]
        public void Harvester_LoopsBetweenNodeAndDropOff()
        {
            SpawnDropOff(new float3(-10f, 0f, 0f));
            var node = _world.SpawnNode(new float3(10f, 0f, 0f), _supplies, 100);
            var harvester = SpawnHarvester(float3.zero);

            _world.Order(harvester, OrderType.Gather, node);
            _world.Run(20f);

            var stock = _world.ResourcesOf(1, _supplies);
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
            _world.Run(10f);

            Assert.IsFalse(_world.EntityManager.Exists(node));
            Assert.AreEqual(5, _world.ResourcesOf(1, _supplies));
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(harvester), "no node left, so the order completes");
        }

        [Test]
        public void DepletedNode_HarvesterMovesToNearbyNodeOfSameType()
        {
            SpawnDropOff(new float3(-6f, 0f, 0f));
            var first = _world.SpawnNode(new float3(4f, 0f, 0f), _supplies, 5);
            var second = _world.SpawnNode(new float3(8f, 0f, 4f), _supplies, 100);
            var harvester = SpawnHarvester(float3.zero);

            _world.Order(harvester, OrderType.Gather, first);
            _world.Run(10f);

            Assert.IsFalse(_world.EntityManager.Exists(first));
            Assert.Less(_world.Get<ResourceNode>(second).Amount, 100);
            Assert.AreEqual(second, _world.Get<ActiveOrder>(harvester).Value.Target);
        }

        [Test]
        public void Harvester_DeliversToNearerDropOff_OnceItIsBuilt()
        {
            SpawnDropOff(new float3(-30f, 0f, 0f));
            var site = _world.SpawnBuilding(1, new float3(6f, 0f, 4f), new float2(4f, 4f), complete: false);
            var writer = new EntityManagerWriter(_world.EntityManager, site);
            ResourceDropOffSetup.Add(ref writer);
            var node = _world.SpawnNode(new float3(10f, 0f, 0f), _supplies, 1000);
            var harvester = SpawnHarvester(new float3(8f, 0f, 0f));

            _world.Order(harvester, OrderType.Gather, node);
            Assert.Less(FarthestWest(harvester, 15f), -20f, "an unfinished site takes no cargo");

            _world.EntityManager.SetComponentEnabled<ConstructionProgress>(site, false);
            _world.Run(10f);
            var before = _world.ResourcesOf(1, _supplies);

            Assert.Greater(FarthestWest(harvester, 20f), 0f, "every later trip ends at the finished drop-off");
            Assert.Greater(_world.ResourcesOf(1, _supplies), before + 20, "it keeps delivering there");
        }

        /// <summary>Runs for the given time and returns the lowest x the harvester reached.</summary>
        private float FarthestWest(Entity harvester, float seconds)
        {
            var west = float.MaxValue;
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                _world.Tick();
                west = math.min(west, _world.Get<LocalTransform>(harvester).Position.x);
            }

            return west;
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
    }
}
