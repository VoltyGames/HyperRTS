using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>An enemy unit on a building spot rules it out on the server; friendly units don't.</summary>
    public class PlacementBlockerTests
    {
        private static readonly float3 Spot = new(20f, 0f, 20f);

        private TestWorld _world;
        private ResourceType _supplies;
        private Entity _prefab;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = _world.CreateSupplies();
            _world.AddResources(1, _supplies, 1000);
            _prefab = _world.MakePrefab(_world.SpawnBuilding(0, new float3(90f, 0f, 90f), new float2(6f, 6f)));
            _world.SetCost(_prefab, _supplies, 100);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private bool Places()
        {
            // One tick first, so the spatial index holds the units standing about.
            _world.Tick();
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero), _prefab);
            _world.PlaceBuilding(builder, _prefab, Spot);
            _world.Tick();
            foreach (var site in _world.All<ConstructionProgress>())
            {
                if (_world.Get<Faction>(site).Value == 1)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void EnemyUnitOnTheSpot_RulesItOut()
        {
            _world.SpawnUnit(2, Spot + new float3(2f, 0f, 1f));

            Assert.IsFalse(Places());
        }

        [Test]
        public void FriendlyUnitOnTheSpot_DrivesOff()
        {
            _world.SpawnUnit(1, Spot + new float3(2f, 0f, 1f));

            Assert.IsTrue(Places());
        }

        [Test]
        public void EnemyUnitBesideTheSpot_DoesNotCount()
        {
            _world.SpawnUnit(2, Spot + new float3(8f, 0f, 0f));

            Assert.IsTrue(Places());
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
