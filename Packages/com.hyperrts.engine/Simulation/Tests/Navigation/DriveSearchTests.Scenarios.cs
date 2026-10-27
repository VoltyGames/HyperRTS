using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Mathematics;
using static HyperRTS.Simulation.Tests.TestVehicles;

namespace HyperRTS.Simulation.Tests.Navigation
{
    public partial class DriveSearchTests
    {
        [Test]
        public void Truck_UTurnsInATwelveMetreYard_WithAtMostTwoGearChanges()
        {
            // A yard 12 m deep and 24 m wide, closed to the north, the truck at its mouth facing in.
            _world.SpawnObstacle(new float3(0f, 0f, 13f), new float2(28f, 2f));
            _world.SpawnObstacle(new float3(-13f, 0f, 6f), new float2(2f, 12f));
            _world.SpawnObstacle(new float3(13f, 0f, 6f), new float2(2f, 12f));
            var truck = Spawn(Truck, Wheels, new float3(0f, 0f, 0f), 0f);
            _world.Tick();

            var drive = DriveTo(truck, new float3(0f, 0f, -30f), 60f);

            Assert.IsTrue(drive.Arrived, "arrived");
            Assert.LessOrEqual(drive.GearChanges, 2, "at most two gear changes");
            Assert.LessOrEqual(drive.Deepest, WallSlack, "never touched a wall");
            Assert.AreEqual(0, Stats().Drive.Failures, "every search found its way");
        }

        [Test]
        public void Truck_TakesANinetyDegreeCorner_OnSevenMetreLanes_WithAPlannedReverseLeg()
        {
            // A lane north meets a lane east, both 7 m wide; the west and north are closed.
            _world.SpawnObstacle(new float3(-4f, 0f, -20f), new float2(2f, 50f));
            _world.SpawnObstacle(new float3(15.5f, 0f, 4f), new float2(41f, 2f));
            _world.SpawnObstacle(new float3(22f, 0f, -22f), new float2(36f, 36f));
            var truck = Spawn(Truck, Wheels, new float3(0.5f, 0f, -35f), 0f);
            _world.Tick();

            var drive = DriveTo(truck, new float3(30f, 0f, -0.5f), 60f);

            Assert.IsTrue(drive.Arrived, "arrived");
            Assert.IsTrue(drive.Reversed, "with a reverse leg");
            Assert.LessOrEqual(drive.Deepest, WallSlack, "never touched a wall");
            Assert.AreEqual(0, Stats().Drive.Failures, "every search found its way");
        }

        [Test]
        public void Truck_ParkedNoseToAWallInASlot_BacksOutAndLeaves()
        {
            // A slot 5 m wide between two buildings, closed by a wall half a metre off the truck's nose.
            _world.SpawnObstacle(new float3(-2.5f, 0f, 0f), new float2(1f, 10f));
            _world.SpawnObstacle(new float3(3.5f, 0f, 0f), new float2(1f, 10f));
            _world.SpawnObstacle(new float3(0.5f, 0f, 5.5f), new float2(7f, 1f));
            var truck = Spawn(Truck, Wheels, new float3(0.5f, 0f, 0.5f), 0f);
            _world.Tick();

            var drive = DriveTo(truck, new float3(25f, 0f, -20f), 60f);

            Assert.IsTrue(drive.Arrived, "arrived");
            Assert.IsTrue(drive.Reversed, "backed out");
            Assert.LessOrEqual(drive.Deepest, WallSlack, "never touched a wall");
        }

        [Test]
        public void Tank_WedgedDiagonallyInASlot_BacksOutAndTurns()
        {
            // A slot 6 m wide, the tank across it at 30 degrees with its nose near the closed end.
            _world.SpawnObstacle(new float3(-3.5f, 0f, 0f), new float2(1f, 12f));
            _world.SpawnObstacle(new float3(3.5f, 0f, 0f), new float2(1f, 12f));
            _world.SpawnObstacle(new float3(0f, 0f, 6.5f), new float2(8f, 1f));
            var tank = Spawn(Tank, Tracks, new float3(0f, 0f, 2f), math.radians(30f));
            _world.Tick();

            var drive = DriveTo(tank, new float3(-25f, 0f, -25f), 60f);

            Assert.IsTrue(drive.Arrived, "arrived");
            Assert.IsTrue(drive.Reversed, "backed out");
            Assert.LessOrEqual(drive.Deepest, WallSlack, "never touched a wall");
        }

        [Test]
        public void BentLaneTooNarrowToTurn_LeavesThePathAsDrawn_AndCountsAFailure()
        {
            // A dead-end lane 4 m wide that bends at right angles: the truck's footprint fits, its hull can't turn.
            _world.SpawnObstacle(new float3(-3f, 0f, 11.5f), new float2(2f, 27f));
            _world.SpawnObstacle(new float3(3f, 0f, 8.5f), new float2(2f, 21f));
            _world.SpawnObstacle(new float3(12f, 0f, 18f), new float2(20f, 2f));
            _world.SpawnObstacle(new float3(9f, 0f, 24f), new float2(26f, 2f));
            _world.SpawnObstacle(new float3(23f, 0f, 21f), new float2(2f, 8f));
            var truck = Spawn(Truck, Wheels, new float3(0f, 0f, -20f), 0f);
            _world.Tick();
            _world.MoveTo(truck, new float3(18f, 0f, 21f));
            _world.Tick(frames: 2);

            Assert.AreEqual(PathStatus.Ready, _world.Get<PathState>(truck).Status);
            Assert.Greater(Stats().Drive.Failures, 0, "the search gave up");
            Assert.IsFalse(HasTightWaypoints(truck), "left as drawn");
        }

        [Test]
        public void Truck_TakesANineMetreLane_WhenGoingRoundIsLonger()
        {
            // Two buildings 9 m apart; round them is about 1.3 times the way through.
            _world.SpawnObstacle(new float3(-7f, 0f, 0f), new float2(6f, 30f));
            _world.SpawnObstacle(new float3(8f, 0f, 0f), new float2(6f, 30f));
            var truck = Spawn(Truck, Wheels, new float3(0.5f, 0f, -25f), 0f);
            _world.Tick();
            _world.MoveTo(truck, new float3(0.5f, 0f, 25f));
            _world.Tick(frames: 2);

            foreach (var waypoint in Waypoints(truck))
            {
                Assert.Less(math.abs(waypoint.Position.x - 0.5f), 4.5f, "through the lane");
            }

            var drive = DriveTo(truck, new float3(0.5f, 0f, 25f), 30f);
            Assert.IsTrue(drive.Arrived, "arrived");
            Assert.LessOrEqual(drive.Deepest, WallSlack, "never touched a wall");
        }
    }
}
