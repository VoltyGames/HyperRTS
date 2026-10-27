using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Navigation
{
    public partial class PathTests
    {
        private static readonly NavAgent PlannedTruck = new() { Radius = 1.25f, HullLength = 8f };

        private static PathWaypoint Tight(float x, float z, bool reverse = false) =>
            new() { Position = new float3(x, 0f, z), Reverse = reverse, Tight = true };

        private DynamicBuffer<PathWaypoint> TightPath(params PathWaypoint[] corners)
        {
            var waypoints = _world.EntityManager.AddBuffer<PathWaypoint>(_world.EntityManager.CreateEntity());
            foreach (var corner in corners)
            {
                waypoints.Add(corner);
            }

            return waypoints;
        }

        [Test]
        public void SkipCorners_PassesATightWaypoint_OnlyOnceTouchedOrBehind()
        {
            CreateMatch(200f);
            var grid = _world.Grid();
            var waypoints = TightPath(Tight(0f, 5f), Tight(0f, 10f), Tight(0.5f, 8.5f, true));
            var path = new PathState { LengthPastCorner = PathState.Measure(waypoints) };

            path.SkipCorners(grid, PlannedTruck, float3.zero, 0.6f, 20f, waypoints);
            Assert.AreEqual(3, waypoints.Length, "never cut, however far it sees");

            path.SkipCorners(grid, PlannedTruck, new float3(0.3f, 0f, 5.4f), 0.6f, 20f, waypoints);
            Assert.AreEqual(2, waypoints.Length, "passed once behind");

            path.SkipCorners(grid, PlannedTruck, new float3(0.2f, 0f, 9.5f), 0.6f, 20f, waypoints);
            Assert.AreEqual(2, waypoints.Length, "a gear change isn't passed within the touch");

            path.SkipCorners(grid, PlannedTruck, new float3(0.1f, 0f, 9.8f), 0.6f, 20f, waypoints);
            Assert.AreEqual(1, waypoints.Length, "but on arriving");
        }

        [Test]
        public void TryFollowGoal_NeverMovesATightEnd()
        {
            CreateMatch(200f);
            _world.AddAgentPrefabs(PlannedTruck);
            _world.Tick();
            var access = PlannedTruck.Access(_world.Grid());
            var waypoints = TightPath(Tight(0f, 5f), Tight(0f, 10f));
            var path = new PathState { LengthPastCorner = PathState.Measure(waypoints) };

            var followed = path.TryFollowGoal(access, float3.zero, new float3(0.5f, 0f, 10.2f), waypoints);

            Assert.IsFalse(followed, "searches again instead");
            MathAssert.Near(new float3(0f, 0f, 10f), waypoints[1].Position);
        }
    }
}
