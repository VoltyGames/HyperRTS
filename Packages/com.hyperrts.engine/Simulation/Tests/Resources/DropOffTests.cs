using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Resources.Gathering;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Resources
{
    /// <summary>
    /// Which drop-off a harvester delivers to: chosen once per trip by edge gap and the turn to face it, kept while
    /// it stands, chosen again when it goes, and a finished one nearer replaces a far one on later trips.
    /// </summary>
    public class DropOffTests : SimulationFixture
    {
        protected override byte[] Teams => new byte[] { 1, 2, 1 };

        private const float TruckRadius = 1.3f;
        private const float TurnRadius = 10f;

        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
        }

        private Entity SpawnDropOff(float3 position)
        {
            var building = World.SpawnBuilding(1, position, new float2(4f, 4f));
            World.EntityManager.AddComponent<ResourceDropOff>(building);
            return building;
        }

        private Entity SpawnHarvester(float3 position, float replacementRange = 30f) =>
            World.MakeHarvester(World.SpawnUnit(1, position), 10, 10f, replacementRange);

        // A full wheeled truck facing +x, heading back with its load from a node beside it.
        private Entity SpawnLoadedTruck()
        {
            var truck = SpawnHarvester(float3.zero);
            var agent = new NavAgent
            {
                Radius = TruckRadius, HullLength = 7f, Spacing = UnitAuthoring.DefaultSpacing, Mass = 17f,
                Chassis = new Chassis { Wheelbase = 4f, RearAxle = 1.2f, TurnRadius = TurnRadius, ReverseRun = 6f },
            };
            agent.WallMargin = agent.HullWallMargin;
            World.Set(truck, agent);
            var writer = World.Writer(truck);
            LocomotionAuthoring.Add(writer, new Locomotion
            {
                Acceleration = 3f, Braking = 6f, TurnRate = math.radians(35f), SteerRate = math.radians(45f),
                ReverseSpeed = 3f,
            });
            World.Set(truck, LocalTransform.FromPositionRotation(float3.zero,
                quaternion.RotateY(math.PI * 0.5f)));
            var harvester = World.Get<Harvester>(truck);
            harvester.CargoType = _supplies;
            harvester.CargoAmount = harvester.Capacity;
            World.Set(truck, harvester);
            World.Set(truck, new HarvestState { Phase = HarvestPhase.Returning });
            var node = World.SpawnNode(new float3(0f, 0f, 8f), _supplies, 1000);
            World.Order(truck, OrderType.Gather, node);
            return truck;
        }

        private Entity ChosenDropOff(Entity truck) => World.Get<HarvestState>(truck).DropOff;

        private bool HasDelivered => World.GetResources(1, _supplies) > 0;

        [Test]
        public void TruckFacingAway_KeepsOneDropOff_TurnsLessThanACircle_AndDelivers()
        {
            SpawnDropOff(new float3(-30f, 0f, 20f));
            SpawnDropOff(new float3(-30f, 0f, -20f));
            var truck = SpawnLoadedTruck();
            World.Tick();
            var chosen = ChosenDropOff(truck);
            Assert.AreNotEqual(Entity.Null, chosen, "a drop-off was chosen");

            var turned = 0f;
            var facing = World.Get<LocalTransform>(truck).Forward().xz;
            for (var t = 0f; t < 40f && !HasDelivered; t += TestWorld.FrameTime)
            {
                World.Tick();
                var now = World.Get<LocalTransform>(truck).Forward().xz;
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
            World.Tick();

            Assert.AreEqual(ahead, ChosenDropOff(truck));
        }

        [Test]
        public void DestroyingTheChosenDropOff_MidTrip_ChoosesTheOther()
        {
            var ahead = SpawnDropOff(new float3(30f, 0f, 0f));
            var behind = SpawnDropOff(new float3(-30f, 0f, 0f));
            var truck = SpawnLoadedTruck();
            World.Run(2f);
            Assert.AreEqual(ahead, ChosenDropOff(truck));

            World.Kill(ahead);

            Assert.IsTrue(World.RunUntil(() => ChosenDropOff(truck) == behind, 1f), "the trip goes on to the one left");
            Assert.IsTrue(World.RunUntil(() => HasDelivered, 40f), "and delivers there");
        }

        [Test]
        public void Harvester_DeliversToNearerDropOff_OnceItIsBuilt()
        {
            SpawnDropOff(new float3(-30f, 0f, 0f));
            var site = World.SpawnBuilding(1, new float3(6f, 0f, 4f), new float2(4f, 4f), complete: false);
            World.EntityManager.AddComponent<ResourceDropOff>(site);
            var node = World.SpawnNode(new float3(10f, 0f, 0f), _supplies, 1000);
            var harvester = SpawnHarvester(new float3(8f, 0f, 0f));

            World.Order(harvester, OrderType.Gather, node);
            Assert.Less(FarthestWest(harvester, 15f), -20f, "an unfinished site takes no cargo");

            World.SetEnabled<ConstructionProgress>(site, false);
            World.Run(10f);
            var before = World.GetResources(1, _supplies);

            Assert.Greater(FarthestWest(harvester, 20f), 0f, "every later trip ends at the finished drop-off");
            Assert.Greater(World.GetResources(1, _supplies), before + 20, "it keeps delivering there");
        }

        /// <summary>Runs for the given time and returns the lowest x the harvester reached.</summary>
        private float FarthestWest(Entity harvester, float seconds)
        {
            var west = float.MaxValue;
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                World.Tick();
                west = math.min(west, World.Get<LocalTransform>(harvester).Position.x);
            }

            return west;
        }
    }
}
