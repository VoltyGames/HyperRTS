using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Spatial;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Shared building blocks: setup helpers, spatial index, resources, command lifetime, death.</summary>
    public class FoundationTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private struct Collector : ISpatialVisitor
        {
            public NativeList<Entity> Found;
            public void Visit(in SpatialEntry entry) => Found.Add(entry.Entity);
        }

        [Test]
        public void SpatialIndex_FindsOnlyEntitiesWithinRadius()
        {
            var near = _world.SpawnUnit(1, new float3(3f, 0f, 0f));
            var far = _world.SpawnUnit(2, new float3(40f, 0f, 0f));
            var building = _world.SpawnBuilding(2, new float3(-9f, 0f, 0f), new float2(8f, 8f));

            _world.Tick();
            _world.EntityManager.CompleteAllTrackedJobs();

            using var query = _world.EntityManager.CreateEntityQuery(typeof(SpatialIndex));
            var index = query.GetSingleton<SpatialIndex>();
            var collector = new Collector { Found = new NativeList<Entity>(Allocator.Temp) };
            index.Query(float3.zero, 5f, ref collector);

            Assert.IsTrue(collector.Found.Contains(near));
            Assert.IsTrue(collector.Found.Contains(building), "edge distance counts, not centre distance");
            Assert.IsFalse(collector.Found.Contains(far));
        }

        [Test]
        public void SpatialIndex_FindsEntitiesWiderThanACell_FromBeyondTheirCentreCell()
        {
            var airfield = _world.SpawnBuilding(1, new float3(20f, 0f, 0f), new float2(40f, 40f));

            _world.Tick();
            _world.EntityManager.CompleteAllTrackedJobs();

            using var query = _world.EntityManager.CreateEntityQuery(typeof(SpatialIndex));
            var index = query.GetSingleton<SpatialIndex>();
            var collector = new Collector { Found = new NativeList<Entity>(Allocator.Temp) };
            index.Query(float3.zero, 1f, ref collector);

            Assert.IsTrue(collector.Found.Contains(airfield), "its 20 m radius reaches a query two cells from its centre");
        }

        [Test]
        public void ResourceMath_SpendsOnlyWhenAffordable()
        {
            var supplies = ScriptableObject.CreateInstance<ResourceType>();
            var entity = _world.EntityManager.CreateEntity();
            var stock = _world.EntityManager.AddBuffer<ResourceStock>(entity);
            ResourceMath.Add(stock, supplies, 100);
            var cost = _world.EntityManager.AddBuffer<ResourceCost>(_world.EntityManager.CreateEntity());
            cost.Add(new ResourceCost { Type = supplies, Amount = 60 });
            stock = _world.EntityManager.GetBuffer<ResourceStock>(entity);

            Assert.IsTrue(ResourceMath.TrySpend(stock, cost));
            Assert.AreEqual(40, ResourceMath.GetAmount(stock, supplies));
            Assert.IsFalse(ResourceMath.TrySpend(stock, cost));
            Assert.AreEqual(40, ResourceMath.GetAmount(stock, supplies), "a failed spend leaves the stock alone");

            Object.DestroyImmediate(supplies);
        }

        [Test]
        public void PlayerCommands_AreClearedAfterTheOrderPhase()
        {
            _world.Command(1, new PlayerCommand { Type = CommandType.Custom });
            _world.Tick();

            Assert.AreEqual(0, _world.EntityManager.GetBuffer<PlayerCommand>(_world.Player(1)).Length);
        }

        [Test]
        public void DeathSystem_DestroysEntityAtZeroHealth()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            _world.EntityManager.SetComponentData(unit, new Health { Current = 0f, Max = 100f });

            _world.Tick();

            Assert.IsFalse(_world.EntityManager.Exists(unit));
        }

        [Test]
        public void SpawnHelpers_MatchBakedArchetype()
        {
            var unit = _world.SpawnUnit(1, new float3(0f, 1f, 0f));
            var building = _world.SpawnBuilding(1, float3.zero, new float2(4f, 4f), complete: false);

            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(unit), "units start idle");
            Assert.IsFalse(_world.IsEnabled<Dead>(unit));
            Assert.IsTrue(_world.IsEnabled<ConstructionProgress>(building));
            Assert.AreEqual(1f, _world.Get<LocalTransform>(unit).Position.y);
        }
    }
}
