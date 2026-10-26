using System;
using System.Linq;
using HyperRTS.Simulation.Commands;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Environment
{
    /// <summary>Shared building blocks: spatial index, command lifetime, which worlds run a system, rotation math.</summary>
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

        private NativeList<Entity> QuerySpatialIndex(float radius)
        {
            _world.Tick();
            _world.EntityManager.CompleteAllTrackedJobs();
            using var query = _world.EntityManager.CreateEntityQuery(typeof(SpatialIndex));
            var collector = new Collector { Found = new NativeList<Entity>(Allocator.Temp) };
            query.GetSingleton<SpatialIndex>().Query(float3.zero, radius, ref collector);
            return collector.Found;
        }

        [Test]
        public void SpatialIndex_FindsOnlyEntitiesWithinRadius()
        {
            var near = _world.SpawnUnit(1, new float3(3f, 0f, 0f));
            var far = _world.SpawnUnit(2, new float3(40f, 0f, 0f));
            var building = _world.SpawnBuilding(2, new float3(-9f, 0f, 0f), new float2(8f, 8f));

            var found = QuerySpatialIndex(5f);

            Assert.IsTrue(found.Contains(near));
            Assert.IsTrue(found.Contains(building), "edge distance counts, not centre distance");
            Assert.IsFalse(found.Contains(far));
        }

        [Test]
        public void SpatialIndex_FindsEntitiesWiderThanACell_FromBeyondTheirCentreCell()
        {
            var airfield = _world.SpawnBuilding(1, new float3(20f, 0f, 0f), new float2(40f, 40f));

            Assert.IsTrue(QuerySpatialIndex(1f).Contains(airfield), "its 20 m radius reaches two cells from its centre");
        }

        [Test]
        public void PlayerCommands_AreClearedAfterTheOrderPhase()
        {
            _world.Command(1, new PlayerCommand { Type = CommandType.Stop });
            _world.Tick();

            Assert.AreEqual(0, _world.EntityManager.GetBuffer<PlayerCommand>(_world.Player(1)).Length);
        }

        // Gameplay runs only where the state lives; selection and fog view run where a player looks.
        [TestCase(typeof(UnitCommandSystem), true, true, false)]
        [TestCase(typeof(SelectionSystem), true, false, true)]
        [TestCase(typeof(FogOfWarSystem), true, true, true)]
        [TestCase(typeof(LocalFogViewSystem), true, false, true)]
        public void SystemRunsInExpectedWorlds(Type system, bool local, bool server, bool client)
        {
            Assert.AreEqual(local, RunsIn(system, WorldSystemFilterFlags.LocalSimulation), "local");
            Assert.AreEqual(server, RunsIn(system, WorldSystemFilterFlags.ServerSimulation), "server");
            Assert.AreEqual(client, RunsIn(system, WorldSystemFilterFlags.ClientSimulation), "client");
        }

        private static bool RunsIn(Type system, WorldSystemFilterFlags world) =>
            DefaultWorldInitialization.GetAllSystems(world).Contains(system);

        [Test]
        public void FromTo_TurnsOneVectorOntoTheOther_EvenWhenTheyAreParallel()
        {
            var from = math.normalize(new float3(1f, 2f, -0.5f));
            var to = math.normalize(new float3(-0.3f, 0.1f, 1f));
            MathAssert.Near(to, math.rotate(Rotations.FromTo(from, to), from), 1e-4f);

            var same = Rotations.FromTo(math.up(), math.up());
            Assert.Greater(math.abs(math.dot(same.value, quaternion.identity.value)), 0.9999f, "same vector");

            var flip = Rotations.FromTo(math.up(), math.down());
            Assert.Greater(math.abs(math.dot(flip.value, quaternion.AxisAngle(math.right(), math.PI).value)), 0.9999f,
                "opposite vectors turn halfway about a perpendicular");
            MathAssert.Near(math.left(), math.rotate(Rotations.FromTo(math.right(), math.left()), math.right()), 1e-4f);
        }
    }
}
