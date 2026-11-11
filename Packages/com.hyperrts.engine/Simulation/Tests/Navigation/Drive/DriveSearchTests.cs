using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Paths;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using static HyperRTS.Simulation.Tests.TestVehicles;

namespace HyperRTS.Simulation.Tests.Navigation.Drive
{
    /// <summary>
    /// The drive planner: easy paths run no search, while yards, tight corners, slots and wedges get planned
    /// manoeuvres with reverse legs that never touch a wall. The search's shortcut and cap.
    /// </summary>
    public class DriveSearchTests : DriveSearchFixture
    {
        [Test]
        public void Shortcut_EndsAnOpenGroundSearch_WithinFiftyExpansions()
        {
            var access = RegisteredAccess(Truck);
            var search = new DriveSearch(access, Truck, DriveSearch.MaxExpansions, Allocator.Temp);
            var points = new NativeList<PathWaypoint>(64, Allocator.Temp);
            var start = Truck.Chassis.Pose(float2.zero, new float2(0f, 1f));

            var found = search.Run(start, DriveGoal.ToPose(new float2(20f, 30f), new float2(1f, 0f)), 0f, points);

            Assert.IsTrue(found, "found");
            Assert.LessOrEqual(search.Expansions, 50, "the curve ended it");
            Assert.Less(math.distance(points[points.Length - 1].Position.xz, new float2(20f, 30f)), 0.01f, "at the goal");
            points.Dispose();
            search.Dispose();
        }

        [Test]
        public void UnreachablePose_StopsAtTheCap()
        {
            // A courtyard walled all round, too small to hold the truck.
            World.SpawnObstacle(new float3(30f, 0f, 36f), new float2(14f, 2f));
            World.SpawnObstacle(new float3(30f, 0f, 24f), new float2(14f, 2f));
            World.SpawnObstacle(new float3(24f, 0f, 30f), new float2(2f, 14f));
            World.SpawnObstacle(new float3(36f, 0f, 30f), new float2(2f, 14f));
            var access = RegisteredAccess(Truck);
            var search = new DriveSearch(access, Truck, DriveSearch.MaxExpansions, Allocator.Temp);
            var points = new NativeList<PathWaypoint>(64, Allocator.Temp);
            var start = Truck.Chassis.Pose(float2.zero, new float2(0f, 1f));

            var found = search.Run(start, DriveGoal.ToPose(new float2(30f, 30f), new float2(1f, 0f)), 0f, points);

            Assert.IsFalse(found, "no way in");
            Assert.AreEqual(DriveSearch.MaxExpansions, search.Expansions, "stopped at the cap");
            Assert.AreEqual(0, points.Length, "added nothing");
            points.Dispose();
            search.Dispose();
        }

        [Test]
        public void OpenGround_PathHasNoTightWaypoints_AndRunsNoSearch()
        {
            var truck = Spawn(Truck, Wheels, float3.zero, 0f);
            World.Tick();
            World.MoveTo(truck, new float3(40f, 0f, 70f));
            World.Tick(frames: 2);

            Assert.AreEqual(PathStatus.Ready, World.Get<PathState>(truck).Status);
            Assert.IsFalse(HasTightWaypoints(truck), "drawn as planned");
            Assert.AreEqual(0, World.ReadPathStats().Drive.Searches, "no search");
        }
    }
}
