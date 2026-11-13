using HyperRTS.Simulation.Navigation.Agents;
using NUnit.Framework;
using Unity.Mathematics;
using static HyperRTS.Simulation.Tests.TestVehicles;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Trucks and tanks turning round and backing out in yards, slots and lanes.</summary>
    public class TightSpaceTests : TightSpaceFixture
    {
        [Test]
        public void Truck_UTurnsInATwelveMetreYard_WithAtMostTwoGearChanges()
        {
            // A yard 12 m deep and 24 m wide, closed to the north, the truck at its mouth facing in.
            World.SpawnObstacle(new float3(0f, 0f, 13f), new float2(28f, 2f));
            World.SpawnObstacle(new float3(-13f, 0f, 6f), new float2(2f, 12f));
            World.SpawnObstacle(new float3(13f, 0f, 6f), new float2(2f, 12f));
            var truck = Spawn(Truck, Wheels, new float3(0f, 0f, 0f), 0f);
            World.Tick();

            var drive = DriveTo(truck, new float3(0f, 0f, -30f), 60f);

            Assert.IsTrue(drive.Arrived, "arrived");
            Assert.LessOrEqual(drive.GearChanges, 2, "at most two gear changes");
            Assert.LessOrEqual(drive.Deepest, WallSlack, "never touched a wall");
        }

        [Test]
        public void Truck_TakesANinetyDegreeCorner_OnSevenMetreLanes()
        {
            // A lane north meets a lane east, both 7 m wide; the west and north are closed.
            World.SpawnObstacle(new float3(-4f, 0f, -20f), new float2(2f, 50f));
            World.SpawnObstacle(new float3(15.5f, 0f, 4f), new float2(41f, 2f));
            World.SpawnObstacle(new float3(22f, 0f, -22f), new float2(36f, 36f));
            var truck = Spawn(Truck, Wheels, new float3(0.5f, 0f, -35f), 0f);
            World.Tick();

            var drive = DriveTo(truck, new float3(30f, 0f, -0.5f), 60f);

            Assert.IsTrue(drive.Arrived, "arrived");
            Assert.LessOrEqual(drive.Deepest, WallSlack, "never touched a wall");
        }

        [Test]
        public void Truck_ParkedNoseToAWallInASlot_BacksOutAndLeaves()
        {
            // A slot 5 m wide between two buildings, closed by a wall half a metre off the truck's nose.
            World.SpawnObstacle(new float3(-2.5f, 0f, 0f), new float2(1f, 10f));
            World.SpawnObstacle(new float3(3.5f, 0f, 0f), new float2(1f, 10f));
            World.SpawnObstacle(new float3(0.5f, 0f, 5.5f), new float2(7f, 1f));
            var truck = Spawn(Truck, Wheels, new float3(0.5f, 0f, 0.5f), 0f);
            World.Tick();

            var drive = DriveTo(truck, new float3(25f, 0f, -20f), 60f);

            Assert.IsTrue(drive.Arrived, "arrived");
            Assert.IsTrue(drive.Reversed, "backed out");
            Assert.LessOrEqual(drive.Deepest, WallSlack, "never touched a wall");
        }

        [Test]
        public void Tank_WedgedDiagonallyInASlot_BacksOutAndTurns()
        {
            // A slot 6 m wide, the tank across it at 30 degrees with its nose near the closed end.
            World.SpawnObstacle(new float3(-3.5f, 0f, 0f), new float2(1f, 12f));
            World.SpawnObstacle(new float3(3.5f, 0f, 0f), new float2(1f, 12f));
            World.SpawnObstacle(new float3(0f, 0f, 6.5f), new float2(8f, 1f));
            var tank = Spawn(Tank, Tracks, new float3(0f, 0f, 2f), math.radians(30f));
            World.Tick();

            var drive = DriveTo(tank, new float3(-25f, 0f, -25f), 60f);

            Assert.IsTrue(drive.Arrived, "arrived");
            Assert.IsTrue(drive.Reversed, "backed out");
            Assert.LessOrEqual(drive.Deepest, WallSlack, "never touched a wall");
        }

        [Test]
        public void Truck_TakesANineMetreLane_WhenGoingRoundIsLonger()
        {
            // Two buildings 9 m apart; round them is about 1.3 times the way through.
            World.SpawnObstacle(new float3(-7f, 0f, 0f), new float2(6f, 30f));
            World.SpawnObstacle(new float3(8f, 0f, 0f), new float2(6f, 30f));
            var truck = Spawn(Truck, Wheels, new float3(0.5f, 0f, -25f), 0f);
            World.Tick();
            World.MoveTo(truck, new float3(0.5f, 0f, 25f));
            World.Tick(frames: 2);

            foreach (var waypoint in World.Buffer<PathWaypoint>(truck))
            {
                Assert.Less(math.abs(waypoint.Position.x - 0.5f), 4.5f, "through the lane");
            }

            var drive = DriveTo(truck, new float3(0.5f, 0f, 25f), 30f);
            Assert.IsTrue(drive.Arrived, "arrived");
            Assert.LessOrEqual(drive.Deepest, WallSlack, "never touched a wall");
        }
    }
}
