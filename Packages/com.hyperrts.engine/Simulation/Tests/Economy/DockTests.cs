using System.Collections.Generic;
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
    /// Dock bays: trucks share them and wait in line, stop square to the bay, free it when they die or are ordered
    /// away, and units without a chassis walk to the edge as before.
    /// </summary>
    public class DockTests
    {
        private const float TruckRadius = 1.3f;
        private const float TruckLength = 7f;
        private static readonly float3 DepotCenter = new(30f, 0f, 0f);

        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2, 1);
            _supplies = _world.CreateSupplies();
            _world.SpawnNode(new float3(-40f, 0f, 0f), _supplies, 1000);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        // A 10 m drop-off with a bay 3 m off its south wall driven through east, and one off its north wall driven
        // through west.
        private Entity SpawnDepot(int bayCount)
        {
            var depot = _world.SpawnBuilding(1, DepotCenter, new float2(10f, 10f));
            _world.EntityManager.AddComponent<ResourceDropOff>(depot);
            var bays = new List<DockBay> { Bay(new float3(0f, 0f, -8f), new float2(1f, 0f)) };
            if (bayCount > 1)
            {
                bays.Add(Bay(new float3(0f, 0f, 8f), new float2(-1f, 0f)));
            }

            var writer = new EntityManagerWriter(_world.EntityManager, depot);
            DockAuthoring.Add(ref writer, "Depot", bays);
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
            var truck = SpawnLoaded(_world.SpawnUnit(1, position, 8f, TruckRadius));
            var agent = new NavAgent
            {
                Radius = TruckRadius, HullLength = TruckLength, Spacing = UnitAuthoring.DefaultSpacing,
                Chassis = new Chassis { Wheelbase = 4f, RearAxle = 1.2f, TurnRadius = 10f, ReverseRun = 6f },
            };
            agent.WallMargin = agent.HullWallMargin;
            _world.EntityManager.SetComponentData(truck, agent);
            var writer = new EntityManagerWriter(_world.EntityManager, truck);
            LocomotionAuthoring.Add(ref writer, new Locomotion
            {
                Acceleration = 3f, Braking = 6f, TurnRate = math.radians(35f), SteerRate = math.radians(45f),
                ReverseSpeed = 3f,
            });
            _world.EntityManager.SetComponentData(truck, LocalTransform.FromPositionRotation(position,
                quaternion.RotateY(math.PI * 0.5f)));
            return truck;
        }

        private Entity SpawnLoaded(Entity unit)
        {
            _world.MakeHarvester(unit, 10, 10f);
            var harvester = _world.Get<Harvester>(unit);
            harvester.CargoType = _supplies;
            harvester.CargoAmount = harvester.Capacity;
            _world.EntityManager.SetComponentData(unit, harvester);
            _world.EntityManager.SetComponentData(unit, new HarvestState { Phase = HarvestPhase.Returning });
            _world.Order(unit, OrderType.Gather, _world.All<ResourceNode>()[0]);
            return unit;
        }

        private bool HasDelivered(Entity unit) => _world.Get<Harvester>(unit).CargoAmount == 0;

        private Entity Occupant(Entity depot, int bay) => _world.EntityManager.GetBuffer<DockBay>(depot)[bay].Occupant;

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
                _world.Tick();
                for (var i = 0; i < trucks.Length; i++)
                {
                    delivered[i] |= HasDelivered(trucks[i]);
                    waited |= _world.Get<HarvestState>(trucks[i]).Place > 0;
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
            Assert.IsTrue(_world.RunUntil(() => HasDelivered(truck), 60f), "the load was delivered");

            var transform = _world.Get<LocalTransform>(truck);
            var stop = DepotCenter + new float3(0f, 0f, -8f);
            var heading = math.degrees(math.acos(math.clamp(transform.Forward().x, -1f, 1f)));
            Assert.Less(heading, 15f, "it stands within 15 degrees of the bay's heading");
            Assert.Less(math.distance(transform.Position.xz, stop.xz), BayPose.StopReach, "at the stop");
        }

        [Test]
        public void Bay_Frees_WhenItsTruckDies()
        {
            var depot = SpawnDepot(1);
            var truck = SpawnTruck(new float3(0f, 0f, -20f));
            Assert.IsTrue(_world.RunUntil(() => Occupant(depot, 0) == truck, 2f), "the truck claimed the bay");

            _world.Kill(truck);

            Assert.IsTrue(_world.RunUntil(() => Occupant(depot, 0) == Entity.Null, 1f), "the bay is free again");
        }

        [Test]
        public void Bay_Frees_WhenItsTruckGetsAnotherOrder()
        {
            var depot = SpawnDepot(1);
            var truck = SpawnTruck(new float3(0f, 0f, -20f));
            Assert.IsTrue(_world.RunUntil(() => Occupant(depot, 0) == truck, 2f), "the truck claimed the bay");

            _world.Issue(truck, new Order { Type = OrderType.Move, Position = new float3(-20f, 0f, 30f) });

            Assert.IsTrue(_world.RunUntil(() => Occupant(depot, 0) == Entity.Null, 1f), "the bay is free again");
        }

        [Test]
        public void InfantryAndAircraft_IgnoreBays_AndDeliverAtTheEdge()
        {
            var depot = SpawnDepot(2);
            var soldier = SpawnLoaded(_world.SpawnUnit(1, new float3(10f, 0f, 0f)));
            var aircraft = _world.SpawnAircraft(1, new float3(10f, 0f, 20f));
            _world.EntityManager.SetComponentData(aircraft,
                new NavAgent { Radius = 1f, HullLength = 6f, Layer = NavLayer.Air });
            SpawnLoaded(aircraft);

            var claimed = false;
            for (var t = 0f; t < 30f && !(HasDelivered(soldier) && HasDelivered(aircraft)); t += TestWorld.FrameTime)
            {
                _world.Tick();
                claimed |= Occupant(depot, 0) != Entity.Null || Occupant(depot, 1) != Entity.Null;
            }

            Assert.IsTrue(HasDelivered(soldier), "the soldier delivered");
            Assert.IsTrue(HasDelivered(aircraft), "the aircraft delivered");
            Assert.IsFalse(claimed, "neither took a bay");
        }

        // The least gap between two hulls, from points along one's core segment to the other's capsule.
        private float HullGap(Entity a, Entity b)
        {
            var agentA = _world.Get<NavAgent>(a);
            var agentB = _world.Get<NavAgent>(b);
            var transformA = _world.Get<LocalTransform>(a);
            var transformB = _world.Get<LocalTransform>(b);
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
