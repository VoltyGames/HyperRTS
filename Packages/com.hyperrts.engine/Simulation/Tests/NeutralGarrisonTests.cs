using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>A neutral garrison building (a village house): boarding as a group, selling and unloading.</summary>
    public class NeutralGarrisonTests
    {
        private static readonly float2 Footprint = new(10f, 9f);

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        /// <summary>Maps use coarser nav cells than the engine default.</summary>
        private void SetNavCellSize(float size)
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(MapSettings));
            var map = query.GetSingleton<MapSettings>();
            map.NavCellSize = size;
            query.SetSingleton(map);
        }

        private Entity House(float3 position, float yawDegrees = 0f, int capacity = 4)
        {
            var house = _world.SpawnBuilding(Faction.Neutral, position, Footprint);
            var turned = LocalTransform.FromPositionRotation(position, quaternion.RotateY(math.radians(yawDegrees)));
            _world.EntityManager.SetComponentData(house, turned);
            var writer = new EntityManagerWriter(_world.EntityManager, house);
            TransportSetup.AddContainer(ref writer, new Container
            {
                Capacity = capacity, MaxPassengerSize = 1, PassengersFire = true, PassengersSurvive = true,
            });
            writer.Add<NeutralGarrison>();
            writer.Add(new Destructible());
            return house;
        }

        private Entity Rifleman(byte faction, float3 position)
        {
            var unit = _world.SpawnUnit(faction, position);
            var writer = new EntityManagerWriter(_world.EntityManager, unit);
            TransportSetup.AddPassenger(ref writer, 1);
            _world.AddWeapon(unit, range: 7f, damage: 8f);
            return unit;
        }

        private int Occupants(Entity house) => _world.EntityManager.GetBuffer<Cargo>(house).Length;

        private byte Owner(Entity house) => _world.Get<Faction>(house).Value;

        /// <summary>
        /// Grids as the maps build them (2 m cells) park a unit up to three metres from a footprint, depending on
        /// where the footprint falls on the grid, and village houses stand at any angle.
        /// </summary>
        [TestCase(1f, 0f, 0f)]
        [TestCase(2f, 0f, 0f)]
        [TestCase(2f, 0.5f, 0f)]
        [TestCase(2f, 1f, 0f)]
        [TestCase(2f, 1.5f, 0f)]
        [TestCase(1f, 0f, 45f)]
        [TestCase(2f, 0f, 45f)]
        [TestCase(2f, 0.5f, 30f)]
        [TestCase(2f, 1f, 70f)]
        public void Garrison_TakesEveryUnitOfAGroup(float navCellSize, float offset, float yawDegrees)
        {
            SetNavCellSize(navCellSize);
            var center = new float3(offset, 0f, offset * 0.7f);
            var house = House(center, yawDegrees);
            var first = Rifleman(1, center + new float3(14f, 0f, 0f));
            var second = Rifleman(1, center + new float3(18f, 0f, 1f));
            var third = Rifleman(1, center + new float3(22f, 0f, -1f));
            _world.Enter(first, house);
            _world.Enter(second, house);
            _world.Enter(third, house);

            _world.RunUntil(() => Occupants(house) == 3 && Owner(house) == 1, 20f);

            Assert.AreEqual(3, Occupants(house));
            Assert.IsTrue(_world.IsInside(first) && _world.IsInside(second) && _world.IsInside(third));
            Assert.AreEqual(1, Owner(house), "it belongs to its garrison");
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void Garrison_TakesAnotherUnitOnceItIsOwned(float navCellSize)
        {
            SetNavCellSize(navCellSize);
            var house = House(float3.zero);
            var first = Rifleman(1, new float3(10f, 0f, 0f));
            _world.Enter(first, house);
            _world.Run(5f);
            Assert.AreEqual(1, Owner(house));

            var second = Rifleman(1, new float3(10f, 0f, 3f));
            _world.Enter(second, house);
            _world.Run(5f);

            Assert.IsTrue(_world.IsInside(second));
            Assert.AreEqual(2, Occupants(house));
        }

        [Test]
        public void Garrison_RefusesAnEnemy_WhileOwned()
        {
            var house = House(float3.zero);
            var first = Rifleman(1, new float3(10f, 0f, 0f));
            _world.Enter(first, house);
            _world.Run(5f);

            var enemy = Rifleman(2, new float3(10f, 0f, 3f));
            _world.Command(2, new PlayerCommand { Type = CommandType.Enter, Unit = enemy, Target = house });
            _world.Run(5f);

            Assert.IsFalse(_world.IsInside(enemy));
            Assert.AreEqual(1, Occupants(house));
        }

        [Test]
        public void IsSellable_OnlyForBuildingsThatAreNotGarrisons()
        {
            var depot = _world.SpawnBuilding(1, float3.zero, Footprint);
            var house = House(new float3(30f, 0f, 0f));
            var unit = Rifleman(1, new float3(60f, 0f, 0f));
            var entityManager = _world.EntityManager;

            Assert.IsTrue(BuildingRules.IsSellable(entityManager, depot));
            Assert.IsFalse(BuildingRules.IsSellable(entityManager, house), "occupying a house doesn't make it yours to sell");
            Assert.IsFalse(BuildingRules.IsSellable(entityManager, unit));
        }

        [Test]
        public void Sell_LeavesAGarrisonItsOccupantsOwn()
        {
            var house = House(float3.zero);
            var rifleman = Rifleman(1, new float3(10f, 0f, 0f));
            _world.Enter(rifleman, house);
            _world.Run(5f);
            Assert.AreEqual(1, Owner(house));

            _world.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = house });
            _world.Tick(frames: 3);

            Assert.IsTrue(_world.EntityManager.Exists(house));
            Assert.IsTrue(_world.IsInside(rifleman));
            Assert.AreEqual(1, Owner(house));
        }

        [Test]
        public void Unload_ReturnsTheHouseToNeutral_AndEveryoneOutside()
        {
            SetNavCellSize(2f);
            var house = House(float3.zero, capacity: 6);
            var garrison = new Entity[6];
            for (var i = 0; i < garrison.Length; i++)
            {
                garrison[i] = Rifleman(1, new float3(10f, 0f, i * 2f - 5f));
                _world.Enter(garrison[i], house);
            }

            _world.RunUntil(() => Occupants(house) == garrison.Length, 12f);
            Assert.AreEqual(garrison.Length, Occupants(house));
            _world.EntityManager.SetComponentEnabled<Selected>(house, true);

            _world.Command(1, new PlayerCommand { Type = CommandType.Unload, Unit = house, Argument = -1 });
            _world.Tick(frames: 3);

            Assert.AreEqual(0, Occupants(house));
            Assert.AreEqual(Faction.Neutral, Owner(house), "empty, it is nobody's again");
            Assert.IsFalse(_world.IsEnabled<Selected>(house), "a neutral building isn't a selection");
            for (var i = 0; i < garrison.Length; i++)
            {
                AssertStandsBesideTheHouse(garrison[i]);
                for (var j = i + 1; j < garrison.Length; j++)
                {
                    var apart = math.distance(_world.GetPosition(garrison[i]).xz, _world.GetPosition(garrison[j]).xz);
                    Assert.Greater(apart, 1f, "passengers don't share a spot");
                }
            }

            _world.Run(5f);
            Assert.AreEqual(0, Occupants(house), "nobody walks back in");
            foreach (var unit in garrison)
            {
                AssertStandsBesideTheHouse(unit);
            }
        }

        private void AssertStandsBesideTheHouse(Entity unit)
        {
            Assert.IsFalse(_world.IsInside(unit), "got out");
            var position = _world.GetPosition(unit);
            Assert.IsTrue(math.all(math.isfinite(position)), "has a position");
            var outside = math.abs(position.x) >= Footprint.x * 0.5f || math.abs(position.z) >= Footprint.y * 0.5f;
            Assert.IsTrue(outside, $"stands outside the walls, was at {position}");
            Assert.Less(math.length(position.xz), 12f, "stays next to the house");
            Assert.IsFalse(_world.IsEnabled<Dead>(unit));
            Assert.IsFalse(_world.EntityManager.HasComponent<FogHidden>(unit), "its own side sees it");
            using var query = _world.EntityManager.CreateEntityQuery(typeof(NavGrid));
            var grid = query.GetSingleton<NavGrid>();
            Assert.IsTrue(grid.IsWalkable(position), "stands on open ground");
        }
    }
}
