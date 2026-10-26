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

namespace HyperRTS.Simulation.Tests.Placement
{
    /// <summary>
    /// Placing a building: quarter turns snap edges onto cells, free yaws land on the half-cell lattice, turned
    /// neighbours sit flush, the yaw survives the wire, and only enemy units on the spot rule it out.
    /// </summary>
    public class BuildingPlacementTests
    {
        private static readonly float3 Far = new(90f, 0f, 90f);
        private static readonly float3 SpawnOffset = new(0f, 0f, 4f);

        // Right along (0.8, -0.6), so a neighbour one width over sits on the half-cell lattice.
        private static readonly float TiltedYaw = math.atan2(0.6f, 0.8f);

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

        private static float Quarter(int turn) => PlacementMath.CameraYaw + math.PI * 0.5f * turn;

        private void Command(Entity prefab, float3 position, float yaw)
        {
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero), prefab);
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = position, Yaw = yaw,
            });
        }

        private Entity Place(Entity prefab, float3 position, float yaw)
        {
            Command(prefab, position, yaw);
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

        [TestCase(0, 0f, -1f, 10.5f, 10f)]
        [TestCase(1, -1f, 0f, 10f, 10.5f)]
        [TestCase(2, 0f, 1f, 10.5f, 10f)]
        [TestCase(3, 1f, 0f, 10f, 10.5f)]
        public void QuarterTurn_TurnsTheFrontFootprintAndSpawnPoint(int turn, float frontX, float frontZ,
            float centerX, float centerZ)
        {
            var site = Place(FactoryPrefab(new float2(3f, 2f)), new float3(10.2f, 0f, 10.2f), Quarter(turn));
            var transform = _world.Get<LocalTransform>(site);
            var front = new float3(frontX, 0f, frontZ);

            MathAssert.Near(front, Front(site));
            Assert.AreEqual(centerX, transform.Position.x, 1e-4f, "snapped to the turned footprint");
            Assert.AreEqual(centerZ, transform.Position.z, 1e-4f);
            var box = new NavBox(transform.Position, transform.Rotation, _world.Get<NavObstacle>(site).Size);
            var extent = box.Extents * 2f;
            MathAssert.Near(turn % 2 == 1 ? new float2(2f, 3f) : new float2(3f, 2f), extent);
            var spawn = transform.TransformPoint(_world.Get<Producer>(site).SpawnOffset);
            MathAssert.Near(transform.Position + front * SpawnOffset.z, spawn);
        }

        [Test]
        public void TurnedFootprint_DecidesValidity_AtTheMapEdge()
        {
            var prefab = FactoryPrefab(new float2(2f, 6f));

            Assert.AreEqual(Entity.Null, Place(prefab, new float3(98f, 0f, 0f), Quarter(1)), "turned, it crosses the edge");
            Assert.AreNotEqual(Entity.Null, Place(prefab, new float3(98f, 0f, 0f), Quarter(0)), "unturned, it fits");
        }

        [Test]
        public void NonFiniteYaw_IsRefused()
        {
            Assert.AreEqual(Entity.Null, Place(FactoryPrefab(new float2(4f, 4f)), new float3(10f, 0f, 10f), float.NaN));
            Assert.AreEqual(1000, _world.GetResources(1, _supplies), "nothing charged");
        }

        [Test]
        public void FreeYaw_TurnsTheSite_AndCentresItOnTheHalfCellLattice()
        {
            const float yaw = 0.7f;
            var site = Place(FactoryPrefab(new float2(4f, 4f)), new float3(10.3f, 0f, 10.1f), yaw);
            var transform = _world.Get<LocalTransform>(site);

            MathAssert.Near(new float3(math.sin(yaw), 0f, math.cos(yaw)), Front(site));
            Assert.AreEqual(10.5f, transform.Position.x, 1e-4f);
            Assert.AreEqual(10f, transform.Position.z, 1e-4f);
        }

        [TestCase(14f, 7f, 2, TestName = "TurnedNeighbours_ThatOnlyTouch_AreBothPlaced")]
        [TestCase(12f, 8.5f, 1, TestName = "TurnedNeighbours_ThatOverlap_AreRefused")]
        public void TurnedNeighbours_AreComparedAsTurnedBoxes(float x, float z, int expected)
        {
            var prefab = FactoryPrefab(new float2(5f, 5f));
            Command(prefab, new float3(10f, 0f, 10f), TiltedYaw);
            Command(prefab, new float3(x, 0f, z), TiltedYaw);
            _world.Tick();

            Assert.AreEqual(expected, PlayerSites());
        }

        private int PlayerSites()
        {
            var count = 0;
            foreach (var site in _world.All<ConstructionProgress>())
            {
                count += _world.Get<Faction>(site).Value == 1 ? 1 : 0;
            }

            return count;
        }

        [Test]
        public void FreeYaw_RoundTripsThroughTheCommandRpc()
        {
            var command = new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Position = new float3(3f, 0f, 7f), Yaw = 0.7f, Queue = true,
            };

            var received = CommandRpc.From(command).ToCommand();

            Assert.AreEqual(CommandType.PlaceBuilding, received.Type);
            Assert.AreEqual(0.7f, received.Yaw);
            Assert.AreEqual(command.Position, received.Position);
            Assert.IsTrue(received.Queue);
        }

        [TestCase(2, 2f, 1f, false, TestName = "EnemyUnitOnTheSpot_RulesItOut")]
        [TestCase(1, 2f, 1f, true, TestName = "FriendlyUnitOnTheSpot_DrivesOff")]
        [TestCase(2, 8f, 0f, true, TestName = "EnemyUnitBesideTheSpot_DoesNotCount")]
        public void UnitOnTheSpot_OnlyAnEnemyRulesItOut(int faction, float x, float z, bool places)
        {
            var spot = new float3(20f, 0f, 20f);
            var prefab = FactoryPrefab(new float2(6f, 6f));
            _world.SpawnUnit((byte)faction, spot + new float3(x, 0f, z));
            // One tick first, so the spatial index holds the unit.
            _world.Tick();

            Assert.AreEqual(places, Place(prefab, spot, Quarter(0)) != Entity.Null);
        }

        [Test]
        public void TurnedSpot_ChecksTheUnitCapsuleAgainstTheTurnedBox()
        {
            var spot = new BuildingSpot(float3.zero, math.PI * 0.25f, new float2(4f, 4f), PlacementSurface.Land);
            var axis = new float2(1f, 0f);

            // A box corner reaches 2.83 m along x at 45 degrees; the capsule's near end stops short of it.
            Assert.IsFalse(spot.Overlaps(new float2(4.5f, 0f), axis, 0.5f));
            Assert.IsTrue(spot.Overlaps(new float2(3.5f, 0f), axis, 0.5f));
        }
    }
}
