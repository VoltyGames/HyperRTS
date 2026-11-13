using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Spatial;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>
    /// Vehicles and foot units sharing ground: the crowd's push never moves a vehicle, and allied foot units step
    /// out of a vehicle's lane before it reaches them, or are driven through once they hold it 2 s.
    /// </summary>
    public class RightOfWayTests : SimulationFixture
    {
        private const float East = math.PI * 0.5f;

        // Owners 1 and 2 are teammates, so a teammate's squad needs no second match.
        protected override byte[] Teams => new byte[] { 1, 1 };

        private Entity SpawnTruck(float3 position) =>
            World.SpawnVehicle(1, TestVehicles.At(position, East), 6f, TestVehicles.SupplyTruckHull(),
                TestVehicles.SupplyTruck);

        private float Speed(Entity unit) => math.length(World.Get<Velocity>(unit).Value);

        // Metres between the vehicle's hull and the foot unit's circle; negative once they overlap.
        private float HullGap(Entity vehicle, Entity unit)
        {
            var agent = World.Get<NavAgent>(vehicle);
            var axis = agent.SegmentAxis(World.Get<LocalTransform>(vehicle).Rotation);
            var offset = Planar.ClosestOffset(World.GetPosition(vehicle).xz, axis, World.GetPosition(unit).xz,
                float2.zero);
            return math.length(offset) - agent.Radius - World.Get<NavAgent>(unit).Radius;
        }

        // A hover hull neither pivots nor has a turn radius, yet has locomotion, so it is still a vehicle.
        [TestCase(true)]
        [TestCase(false)]
        public void IdleTank_IsNotMovedByAWalkingSquad(bool pivots)
        {
            var hull = TestVehicles.TankHull();
            hull.Chassis = pivots ? hull.Chassis : new Chassis { ReverseRun = 10f };
            var tank = World.SpawnVehicle(1, TestVehicles.At(float3.zero, East), 7f, hull, TestVehicles.Tracks);
            var squad = World.SpawnSquad(1, new float3(-10f, 0f, 0.5f));
            World.Tick();
            var goal = new float3(10f, 0f, 0.5f);
            World.MoveTo(squad, goal);

            World.Run(10f);

            Assert.AreEqual(0f, math.length(World.GetPosition(tank).xz), 1e-4f, "the tank stood where it was");
            Assert.Less(math.distance(World.GetPosition(squad).xz, goal.xz), 1f, "the squad walked round it");
        }

        // A teammate's squad gives way just as an own one does.
        [TestCase((byte)1)]
        [TestCase((byte)2)]
        public void IdleSquad_ClearsATrucksLane_BeforeContact(byte squadOwner)
        {
            var squad = World.SpawnSquad(squadOwner, float3.zero);
            var truck = SpawnTruck(new float3(-30f, 0f, 0f));
            World.Tick();
            World.Send(truck, new float3(30f, 0f, 0f));

            var closest = float.MaxValue;
            var stood = 0f;
            var longestStop = 0f;
            for (var t = 0f; t < 40f && World.IsEnabled<MoveDestination>(truck); t += TestWorld.FrameTime)
            {
                World.Tick();
                closest = math.min(closest, HullGap(truck, squad));
                stood = Speed(truck) < 0.1f ? stood + TestWorld.FrameTime : 0f;
                longestStop = math.max(longestStop, stood);
            }

            Assert.IsFalse(World.IsEnabled<MoveDestination>(truck), "the truck arrived");
            Assert.Greater(closest, 0f, $"the squad left the lane before the truck reached it, closest {closest:F2} m");
            Assert.LessOrEqual(longestStop, 1f, $"the truck never stopped for more than a second, not {longestStop:F2} s");
        }

        [Test]
        public void OnlyFootUnitsNearAMovingVehicle_WatchItsLane()
        {
            var truck = SpawnTruck(new float3(-30f, 0f, 0f));
            World.Tick();
            World.Send(truck, new float3(30f, 0f, 0f));
            World.Tick();
            World.EntityManager.CompleteAllTrackedJobs();

            using var query = World.Query(typeof(SpatialIndex));
            var index = query.GetSingleton<SpatialIndex>();
            Assert.IsTrue(index.IsNearVehicleLane(new float3(-30f, 0f, 5f)), "beside the truck");
            Assert.IsFalse(index.IsNearVehicleLane(new float3(-30f, 0f, 60f)), "far from every moving vehicle");
        }

        // A foot unit that can't move holds the truck 2 s; then it drives through at a walk.
        [Test]
        public void FootUnitThatCannotMove_HoldsATruckTwoSeconds_ThenItDrivesThroughAtAWalk()
        {
            var soldier = World.SpawnUnit(1, float3.zero);
            World.Add(soldier, new Pace { Speed = 0f, FireRate = 1f, DamageTaken = 1f });
            var truck = SpawnTruck(new float3(-20f, 0f, 0f));
            World.Tick();
            World.Send(truck, new float3(20f, 0f, 0f));

            var stood = 0f;
            var fastestThrough = 0f;
            for (var t = 0f; t < 40f && World.IsEnabled<MoveDestination>(truck); t += TestWorld.FrameTime)
            {
                World.Tick();
                stood += Speed(truck) < 0.3f ? TestWorld.FrameTime : 0f;
                if (HullGap(truck, soldier) < 0f)
                {
                    fastestThrough = math.max(fastestThrough, Speed(truck));
                }
            }

            Assert.IsFalse(World.IsEnabled<MoveDestination>(truck), "the truck arrived");
            Assert.AreEqual(2f, stood, 0.5f, $"the truck waited about 2 s, not {stood:F2} s");
            Assert.Greater(fastestThrough, 0f, "the truck drove through the soldier");
            Assert.LessOrEqual(fastestThrough, RightOfWay.WalkingPace + 0.05f, $"it drove through at a walk, not {fastestThrough:F2} m/s");
        }
    }
}
