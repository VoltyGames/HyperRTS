using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Spatial;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Shared building blocks: spatial index and command lifetime.</summary>
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
        public void PlayerCommands_AreClearedAfterTheOrderPhase()
        {
            _world.Command(1, new PlayerCommand { Type = CommandType.Custom });
            _world.Tick();

            Assert.AreEqual(0, _world.EntityManager.GetBuffer<PlayerCommand>(_world.Player(1)).Length);
        }
    }
}
