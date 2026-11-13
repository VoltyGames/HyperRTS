using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>A truck sharing ground with idle allied squads and tanks, and with other trucks.</summary>
    [Category("Traffic")]
    public class TrafficMixedTests : TrafficScenarioFixture
    {
        private const float North = 0f;

        [Test]
        public void Truck_DrivesThroughThreeIdleAlliedSquads()
        {
            for (var i = 0; i < 3; i++)
            {
                World.SpawnSquad(1, new float3(-12f + i * 12f, 0f, i % 2 * 3f - 1.5f));
            }

            var truck = TrackedTruck("truck", new float3(-50f, 0f, 0f), East);
            World.Tick();
            World.Send(truck, new float3(50f, 0f, 0f));
            Finish("squads", 90f, 1);
            AssertNoWheelsSlid();
        }

        // Owner 1 is the truck's own side; owner 2 is a teammate, which should make way just the same.
        [TestCase((byte)1, TestName = "Truck_PassesAnIdleOwnTank_InAnEightMetreLane")]
        [TestCase((byte)2, TestName = "Truck_PassesAnIdleTeammatesTank_InAnEightMetreLane")]
        public void Truck_PassesAnIdleAlliedTank_InAnEightMetreLane(byte tankOwner)
        {
            BuildLane(8f);
            SpawnTank(float3.zero, East, tankOwner);
            var truck = TrackedTruck("truck", new float3(-50f, 0f, 0f), East);
            World.Tick();
            World.Send(truck, new float3(50f, 0f, 0f));
            Finish(tankOwner == 1 ? "own tank" : "teammate tank", 90f, tankOwner == 1 ? 1 : 0);
        }

        [Test]
        public void TwoTrucks_MeetHeadOn_InAnEightMetreLane()
        {
            BuildLane(8f);
            var east = TrackedTruck("eastbound", new float3(-50f, 0f, 0f), East);
            var west = TrackedTruck("westbound", new float3(50f, 0f, 0f), West);
            World.Tick();
            World.Send(east, new float3(50f, 0f, 0f));
            World.Send(west, new float3(-50f, 0f, 0f));
            Finish("head-on", 120f, 0);
        }

        // Three trucks east and three north, their lines crossing in the middle of open ground.
        [Test]
        public void SixTrucks_CrossInOpenGround()
        {
            for (var i = 0; i < 3; i++)
            {
                var offset = (i - 1) * 12f;
                TrackedTruck($"east {i}", new float3(-45f, 0f, offset), East);
                TrackedTruck($"north {i}", new float3(offset, 0f, -45f), North);
            }

            World.Tick();
            for (var i = 0; i < 3; i++)
            {
                var offset = (i - 1) * 12f;
                World.Send(_record.Units[i * 2].Unit, new float3(45f, 0f, offset));
                World.Send(_record.Units[i * 2 + 1].Unit, new float3(offset, 0f, 45f));
            }

            Finish("crossing", 90f, 6);
            Assert.Less(_record.MaxLoop, 180f, "crossing traffic yields without turning round");
        }

        // Two walls 60 m long along the x axis, a lane of the given width between them.
        private void BuildLane(float width)
        {
            var centre = width * 0.5f + 10f;
            World.SpawnObstacle(new float3(0f, 0f, centre), new float2(60f, 20f));
            World.SpawnObstacle(new float3(0f, 0f, -centre), new float2(60f, 20f));
        }
    }
}
