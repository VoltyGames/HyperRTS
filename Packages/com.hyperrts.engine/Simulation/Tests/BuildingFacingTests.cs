using HyperRTS.Network.Commands;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// Placed buildings face the camera by default and turn by quarters: footprint swap, snapping, validity, the
    /// producer's spawn point and the turn on the wire.
    /// </summary>
    public class BuildingFacingTests
    {
        private static readonly float3 Far = new(90f, 0f, 90f);
        private static readonly float3 SpawnOffset = new(0f, 0f, 4f);

        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = _world.CreateSupplies();
            _world.AddResources(1, _supplies, 1000);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity FactoryPrefab(float2 footprint)
        {
            var prefab = _world.MakePrefab(_world.SpawnBuilding(0, Far, footprint, name: "Factory"));
            _world.SetCost(prefab, _supplies, 100);
            var unit = _world.UnitPrefab(_supplies, 10);
            return _world.MakeProducer(prefab, SpawnOffset, unit);
        }

        private Entity Place(Entity prefab, float3 position, byte turn)
        {
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero), prefab);
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = position, Turn = turn,
            });
            _world.Tick();
            foreach (var site in _world.All<ConstructionProgress>())
            {
                if (_world.Get<Faction>(site).Value == 1)
                {
                    return site;
                }
            }

            return Entity.Null;
        }

        private float3 Front(Entity site) => math.rotate(_world.Get<LocalTransform>(site).Rotation, math.forward());

        [Test]
        public void DefaultFacing_PointsTheFrontAtTheCamera()
        {
            var site = Place(FactoryPrefab(new float2(4f, 4f)), new float3(10f, 0f, 10f), 0);

            MathAssert.Near(new float3(0f, 0f, -1f), Front(site));
        }

        [TestCase(0, 0f, -1f, 10.5f, 10f)]
        [TestCase(1, -1f, 0f, 10f, 10.5f)]
        [TestCase(2, 0f, 1f, 10.5f, 10f)]
        [TestCase(3, 1f, 0f, 10f, 10.5f)]
        public void QuarterTurn_TurnsTheFrontFootprintAndSpawnPoint(int turn, float frontX, float frontZ,
            float centerX, float centerZ)
        {
            var site = Place(FactoryPrefab(new float2(3f, 2f)), new float3(10.2f, 0f, 10.2f), (byte)turn);
            var transform = _world.Get<LocalTransform>(site);
            var front = new float3(frontX, 0f, frontZ);

            MathAssert.Near(front, Front(site));
            Assert.AreEqual(centerX, transform.Position.x, 1e-4f, "snapped to the turned footprint");
            Assert.AreEqual(centerZ, transform.Position.z, 1e-4f);
            var box = new NavBox(transform.Position, transform.Rotation, _world.Get<NavObstacle>(site).Size);
            var extent = box.Extents * 2f;
            MathAssert.Near(PlacementMath.Turned(new float2(3f, 2f), turn), extent);
            var spawn = transform.TransformPoint(_world.Get<Producer>(site).SpawnOffset);
            MathAssert.Near(transform.Position + front * SpawnOffset.z, spawn);
        }

        [Test]
        public void TurnedFootprint_DecidesValidity_AtTheMapEdge()
        {
            var prefab = FactoryPrefab(new float2(2f, 6f));

            Assert.AreEqual(Entity.Null, Place(prefab, new float3(98f, 0f, 0f), 1), "turned, it crosses the edge");
            Assert.AreNotEqual(Entity.Null, Place(prefab, new float3(98f, 0f, 0f), 0), "unturned, it fits");
        }

        [Test]
        public void TurnOutOfRange_IsRefused()
        {
            Assert.AreEqual(Entity.Null, Place(FactoryPrefab(new float2(4f, 4f)), new float3(10f, 0f, 10f), 4));
            Assert.AreEqual(1000, _world.GetResources(1, _supplies), "nothing charged");
        }

        [Test]
        public void CommandRpc_CarriesTheTurn()
        {
            var command = new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Position = new float3(3f, 0f, 7f), Turn = 3, Queue = true,
            };

            var received = CommandRpc.From(command).ToCommand();

            Assert.AreEqual(CommandType.PlaceBuilding, received.Type);
            Assert.AreEqual(3, received.Turn);
            Assert.AreEqual(command.Position, received.Position);
            Assert.IsTrue(received.Queue);
        }
    }
}
