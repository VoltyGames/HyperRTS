using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Docks;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Grid;
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
    /// Dock bays: trucks share them and wait in line, stop square to the bay, free it when they die or are ordered
    /// away, and units without a chassis walk to the edge as before.
    /// </summary>
    public class DockTests : SimulationFixture
    {
        protected override byte[] Teams => new byte[] { 1, 2, 1 };

        private const float TruckRadius = 1.3f;
        private const float TruckLength = 7f;
        private static readonly float3 DepotCenter = new(30f, 0f, 0f);
        private static readonly float3 SouthStop = DepotCenter + new float3(0f, 0f, -8f);

        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
            World.SpawnNode(new float3(-40f, 0f, 0f), _supplies, 1000);
        }

        // A 10 m drop-off with a bay 3 m off its south wall driven through east, and one off its north wall driven
        // through west.
        private Entity SpawnDepot(int bayCount)
        {
            var depot = World.SpawnBuilding(1, DepotCenter, new float2(10f, 10f));
            World.EntityManager.AddComponent<ResourceDropOff>(depot);
            var bays = new List<DockBay> { Bay(new float3(0f, 0f, -8f), new float2(1f, 0f)) };
            if (bayCount > 1)
            {
                bays.Add(Bay(new float3(0f, 0f, 8f), new float2(-1f, 0f)));
            }

            var writer = World.Writer(depot);
            DockAuthoring.Add(writer, "Depot", bays);
            return depot;
        }

        private static DockBay Bay(float3 stop, float2 facing) => new()
        {
            Stop = stop, Facing = facing, RunIn = DockAuthoring.DefaultRunIn,
            Exit = stop + new float3(facing.x, 0f, facing.y) * DockAuthoring.DefaultExit,
        };

        // A full wheeled truck facing +x, heading back with its load.
        private Entity SpawnTruck(float3 position)
        {
            var truck = SpawnLoaded(World.SpawnUnit(1, position, 8f, TruckRadius));
            var agent = new NavAgent
            {
                Radius = TruckRadius, HullLength = TruckLength, Spacing = UnitAuthoring.DefaultSpacing, Mass = 17f,
                Chassis = new Chassis { Wheelbase = 4f, RearAxle = 1.2f, TurnRadius = 10f, ReverseRun = 6f },
            };
            agent.WallMargin = agent.HullWallMargin;
            World.Set(truck, agent);
            var writer = World.Writer(truck);
            LocomotionAuthoring.Add(writer, new Locomotion
            {
                Acceleration = 3f, Braking = 6f, TurnRate = math.radians(35f), SteerRate = math.radians(45f),
                ReverseSpeed = 3f,
            });
            World.Set(truck, LocalTransform.FromPositionRotation(position,
                quaternion.RotateY(math.PI * 0.5f)));
            return truck;
        }

        private Entity SpawnLoaded(Entity unit)
        {
            World.MakeHarvester(unit, 10, 10f);
            var harvester = World.Get<Harvester>(unit);
            harvester.CargoType = _supplies;
            harvester.CargoAmount = harvester.Capacity;
            World.Set(unit, harvester);
            World.Set(unit, new HarvestState { Phase = HarvestPhase.Returning });
            World.Order(unit, OrderType.Gather, World.All<ResourceNode>()[0]);
            return unit;
        }

        private bool HasDelivered(Entity unit) => World.Get<Harvester>(unit).CargoAmount == 0;

        private Entity Occupant(Entity depot, int bay) => World.Buffer<DockBay>(depot)[bay].Reservation.Holder;

        [Test]
        public void ThreeTrucks_ShareTwoBays_TheThirdWaitsInLine_AndAllDeliverWithoutOverlapping()
        {
            SpawnDepot(2);
            var trucks = new[]
            {
                SpawnTruck(new float3(-5f, 0f, -12f)), SpawnTruck(new float3(-5f, 0f, 0f)),
                SpawnTruck(new float3(-5f, 0f, 12f)),
            };
            var delivered = new bool[trucks.Length];
            var waited = false;
            var worstGap = float.MaxValue;
            for (var t = 0f; t < 90f && System.Array.IndexOf(delivered, false) >= 0; t += TestWorld.FrameTime)
            {
                World.Tick();
                for (var i = 0; i < trucks.Length; i++)
                {
                    delivered[i] |= HasDelivered(trucks[i]);
                    waited |= World.Get<HarvestState>(trucks[i]).Place > 0;
                    for (var j = i + 1; j < trucks.Length; j++)
                    {
                        worstGap = math.min(worstGap, HullGap(trucks[i], trucks[j]));
                    }
                }
            }

            Assert.IsTrue(waited, "one truck waited in line");
            CollectionAssert.AreEqual(new[] { true, true, true }, delivered, "every truck delivered");
            Assert.Greater(worstGap, -0.25f, "no two hulls overlapped");
        }

        [Test]
        public void Truck_ReachesItsStop_SquareToTheBay()
        {
            SpawnDepot(1);
            var truck = SpawnTruck(new float3(0f, 0f, 20f));
            Assert.IsTrue(World.RunUntil(() => HasDelivered(truck), 60f), "the load was delivered");

            var transform = World.Get<LocalTransform>(truck);
            var heading = math.degrees(math.acos(math.clamp(transform.Forward().x, -1f, 1f)));
            Assert.Less(heading, 15f, "it stands within 15 degrees of the bay's heading");
            Assert.Less(math.distance(transform.Position.xz, SouthStop.xz), BayPose.StopReach, "at the stop");
        }

        [Test]
        public void Truck_StoppedAMetreShortOfItsStop_CreepsOnAndDocks()
        {
            // A quarter metre off the bay's line, where the goal sits under the hull inside the turning circle.
            SpawnDepot(1);
            var start = SouthStop + new float3(-1f, 0f, -0.25f);
            var truck = SpawnTruck(start);
            Assert.Greater(math.distance(start.xz, SouthStop.xz), BayPose.StopReach, "it starts short of the stop");

            Assert.IsTrue(World.RunUntil(() => HasDelivered(truck), 15f), "the load was delivered");
            var transform = World.Get<LocalTransform>(truck);
            Assert.Less(math.distance(transform.Position.xz, SouthStop.xz), BayPose.StopReach, "at the stop");
        }

        [Test]
        public void TrucksInLine_StandingAgainstTheirJoiningOrder_TheFrontOneDocksFirst_AndBothDeliver()
        {
            // The far truck joins the line first; the near one joins a frame later, already in front of it.
            var depot = SpawnDepot(1);
            var far = SpawnTruck(new float3(-20f, 0f, -8f));
            HoldBay(depot, 1);
            var near = SpawnTruck(new float3(12f, 0f, -8f));
            HoldBay(depot, 30);
            Assert.Less(World.Get<HarvestState>(far).Place, World.Get<HarvestState>(near).Place, "joined first");

            Assert.IsTrue(World.RunUntil(() => Occupant(depot, 0) == near || Occupant(depot, 0) == far, 5f),
                "a truck took the bay");
            Assert.AreEqual(near, Occupant(depot, 0), "the truck in front docks first");
            Assert.IsTrue(World.RunUntil(() => HasDelivered(near) && HasDelivered(far), 90f), "both delivered");
        }

        // The depot itself holds its bay for these frames, so trucks queue for it.
        private void HoldBay(Entity depot, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                World.Buffer<DockBay>(depot).ElementAt(0).Reservation.Reserve(depot);
                World.Tick();
            }
        }

        [TestCase(true, TestName = "Bay_Frees_WhenItsTruckDies")]
        [TestCase(false, TestName = "Bay_Frees_WhenItsTruckGetsAnotherOrder")]
        public void Bay_Frees_WhenItsTruckLeavesTheJob(bool dies)
        {
            var depot = SpawnDepot(1);
            var truck = SpawnTruck(new float3(0f, 0f, -20f));
            Assert.IsTrue(World.RunUntil(() => Occupant(depot, 0) == truck, 2f), "the truck claimed the bay");

            if (dies)
            {
                World.Kill(truck);
            }
            else
            {
                World.Issue(truck, new Order { Type = OrderType.Move, Position = new float3(-20f, 0f, 30f) });
            }

            Assert.IsTrue(World.RunUntil(() => Occupant(depot, 0) == Entity.Null, 1f), "the bay is free again");
        }

        [Test]
        public void InfantryAndAircraft_IgnoreBays_AndDeliverAtTheEdge()
        {
            var depot = SpawnDepot(2);
            var soldier = SpawnLoaded(World.SpawnUnit(1, new float3(10f, 0f, 0f)));
            var aircraft = World.SpawnAircraft(1, new float3(10f, 0f, 20f));
            World.Set(aircraft,
                new NavAgent { Radius = 1f, HullLength = 6f, Layer = NavLayer.Air });
            SpawnLoaded(aircraft);

            var claimed = false;
            for (var t = 0f; t < 30f && !(HasDelivered(soldier) && HasDelivered(aircraft)); t += TestWorld.FrameTime)
            {
                World.Tick();
                claimed |= Occupant(depot, 0) != Entity.Null || Occupant(depot, 1) != Entity.Null;
            }

            Assert.IsTrue(HasDelivered(soldier), "the soldier delivered");
            Assert.IsTrue(HasDelivered(aircraft), "the aircraft delivered");
            Assert.IsFalse(claimed, "neither took a bay");
        }

        // The least gap between two hulls, from points along one's core segment to the other's capsule.
        private float HullGap(Entity a, Entity b)
        {
            var agentA = World.Get<NavAgent>(a);
            var agentB = World.Get<NavAgent>(b);
            var transformA = World.Get<LocalTransform>(a);
            var transformB = World.Get<LocalTransform>(b);
            var axis = agentB.SegmentAxis(transformB.Rotation);
            var gap = float.MaxValue;
            for (var share = -1f; share <= 1f; share += 0.25f)
            {
                var point = transformB.Position.xz + axis * share;
                gap = math.min(gap, agentA.FootprintGap(transformA.Position.xz, transformA.Rotation, point));
            }

            return gap - agentB.Radius;
        }
    }
}
