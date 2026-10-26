using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Economy
{
    /// <summary>
    /// Which drop-off a harvester delivers to: chosen once per trip by edge gap and the turn to face it, kept while
    /// it stands, chosen again when it goes, and a finished one nearer replaces a far one on later trips.
    /// </summary>
    public class DropOffTests
    {
        private const float TruckRadius = 1.3f;
        private const float TurnRadius = 10f;

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

        private Entity SpawnDropOff(float3 position)
        {
            var building = _world.SpawnBuilding(1, position, new float2(4f, 4f));
            _world.EntityManager.AddComponent<ResourceDropOff>(building);
            return building;
        }

        private Entity SpawnHarvester(float3 position, float replacementRange = 30f) =>
            _world.MakeHarvester(_world.SpawnUnit(1, position), 10, 10f, replacementRange);

        // A full wheeled truck facing +x, heading back with its load from a node beside it.
        private Entity SpawnLoadedTruck()
        {
            var truck = SpawnHarvester(float3.zero);
            var agent = new NavAgent
            {
                Radius = TruckRadius, HullLength = 7f, Spacing = UnitAuthoring.DefaultSpacing,
                Chassis = new Chassis { Wheelbase = 4f, RearAxle = 1.2f, TurnRadius = TurnRadius, ReverseRun = 6f },
            };
            agent.WallMargin = agent.WallMarginFor(TurnRadius);
            _world.EntityManager.SetComponentData(truck, agent);
            var writer = new EntityManagerWriter(_world.EntityManager, truck);
            LocomotionAuthoring.Add(ref writer, new Locomotion
            {
                Acceleration = 3f, Braking = 6f, TurnRate = math.radians(35f), SteerRate = math.radians(45f),
                ReverseSpeed = 3f,
            });
            _world.EntityManager.SetComponentData(truck, LocalTransform.FromPositionRotation(float3.zero,
                quaternion.RotateY(math.PI * 0.5f)));
            var harvester = _world.Get<Harvester>(truck);
            harvester.CargoType = _supplies;
            harvester.CargoAmount = harvester.Capacity;
            _world.EntityManager.SetComponentData(truck, harvester);
            _world.EntityManager.SetComponentData(truck, new HarvestState { Phase = HarvestPhase.Returning });
            var node = _world.SpawnNode(new float3(0f, 0f, 8f), _supplies, 1000);
            _world.Order(truck, OrderType.Gather, node);
            return truck;
        }

        private Entity ChosenDropOff(Entity truck) => _world.Get<HarvestState>(truck).DropOff;

        private bool HasDelivered => _world.GetResources(1, _supplies) > 0;

        [Test]
        public void TruckFacingAway_KeepsOneDropOff_TurnsLessThanACircle_AndDelivers()
        {
            SpawnDropOff(new float3(-30f, 0f, 20f));
            SpawnDropOff(new float3(-30f, 0f, -20f));
            var truck = SpawnLoadedTruck();
            _world.Tick();
            var chosen = ChosenDropOff(truck);
            Assert.AreNotEqual(Entity.Null, chosen, "a drop-off was chosen");

            var turned = 0f;
            var facing = _world.Get<LocalTransform>(truck).Forward().xz;
            for (var t = 0f; t < 40f && !HasDelivered; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var now = _world.Get<LocalTransform>(truck).Forward().xz;
                turned += math.abs(math.atan2(facing.x * now.y - facing.y * now.x, math.dot(facing, now)));
                facing = now;
                if (!HasDelivered)
                {
                    Assert.AreEqual(chosen, ChosenDropOff(truck), "the target never flips mid-trip");
                }
            }

            Assert.IsTrue(HasDelivered, "the load was delivered");
            Assert.Less(turned, math.PI * 2f, "no circling");
        }

        [Test]
        public void Truck_PicksTheDropOffAhead_OverASlightlyNearerOneBehind()
        {
            var ahead = SpawnDropOff(new float3(24f, 0f, 0f));
            SpawnDropOff(new float3(-22f, 0f, 0f));
            var truck = SpawnLoadedTruck();
            _world.Tick();

            Assert.AreEqual(ahead, ChosenDropOff(truck));
        }

        [Test]
        public void DestroyingTheChosenDropOff_MidTrip_ChoosesTheOther()
        {
            var ahead = SpawnDropOff(new float3(30f, 0f, 0f));
            var behind = SpawnDropOff(new float3(-30f, 0f, 0f));
            var truck = SpawnLoadedTruck();
            _world.Run(2f);
            Assert.AreEqual(ahead, ChosenDropOff(truck));

            _world.Kill(ahead);

            Assert.IsTrue(_world.RunUntil(() => ChosenDropOff(truck) == behind, 1f), "the trip goes on to the one left");
            Assert.IsTrue(_world.RunUntil(() => HasDelivered, 40f), "and delivers there");
        }

        [Test]
        public void Harvester_DeliversToNearerDropOff_OnceItIsBuilt()
        {
            SpawnDropOff(new float3(-30f, 0f, 0f));
            var site = _world.SpawnBuilding(1, new float3(6f, 0f, 4f), new float2(4f, 4f), complete: false);
            _world.EntityManager.AddComponent<ResourceDropOff>(site);
            var node = _world.SpawnNode(new float3(10f, 0f, 0f), _supplies, 1000);
            var harvester = SpawnHarvester(new float3(8f, 0f, 0f));

            _world.Order(harvester, OrderType.Gather, node);
            Assert.Less(FarthestWest(harvester, 15f), -20f, "an unfinished site takes no cargo");

            _world.EntityManager.SetComponentEnabled<ConstructionProgress>(site, false);
            _world.Run(10f);
            var before = _world.GetResources(1, _supplies);

            Assert.Greater(FarthestWest(harvester, 20f), 0f, "every later trip ends at the finished drop-off");
            Assert.Greater(_world.GetResources(1, _supplies), before + 20, "it keeps delivering there");
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
    }
}
