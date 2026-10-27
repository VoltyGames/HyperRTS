using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Navigation
{
    public partial class DriveSearchTests
    {
        [Test]
        public void Follow_EndsWhereAdvanceDoes()
        {
            var pose = new AxlePose(new float2(3f, -2f), math.normalize(new float2(1f, 2f)));
            var step = new AxlePose(float2.zero, new float2(1f, 0f)).Advance(0.1f, -1.5f);

            var followed = pose.Follow(step);
            var advanced = pose.Advance(0.1f, -1.5f);

            MathAssert.Near(advanced.Position, followed.Position);
            MathAssert.Near(advanced.Facing, followed.Facing);
        }

        [Test]
        public void CapsuleDepth_SumsEverySunkPoint_AndStopsPastItsLimit()
        {
            _world.SpawnObstacle(new float3(0f, 0f, 3f), new float2(20f, 2f));
            var access = RegisteredAccess(Truck);
            var capsule = new NavCapsule(access, float2.zero);
            var center = new float2(0.3f, 0.8f);
            var axis = math.normalize(new float2(1f, 0.3f)) * Truck.HalfSegment;

            var full = capsule.Depth(center, axis, 0f, float.PositiveInfinity);
            var limited = capsule.Depth(center, axis, 0f, 0.1f);

            var radius = access.Class.Radius;
            var points = 1 + (int)math.ceil(math.length(axis) * 2f / (radius * 0.5f));
            var summed = 0f;
            for (var i = 0; i < points; i++)
            {
                var point = center + axis * (i * 2f / (points - 1) - 1f);
                summed += math.max(0f, radius - access.Distance(point));
            }

            Assert.Greater(summed, 0.5f, "the hull sinks into the wall");
            Assert.AreEqual(summed, full, 1e-4f, "skipping clear points loses nothing");
            Assert.Greater(limited, 0.1f, "past the limit");
            Assert.Less(limited, full, "stopped early");
        }

        [Test]
        public void PoseGoalInALane_IsEnteredFromBehind()
        {
            // A lane 7 m wide along x from 0 to 20, the truck facing in at its east end, the goal facing east inside.
            _world.SpawnObstacle(new float3(10f, 0f, 5f), new float2(20f, 3f));
            _world.SpawnObstacle(new float3(10f, 0f, -5f), new float2(20f, 3f));
            var access = RegisteredAccess(Truck);
            var search = new DriveSearch(access, Truck, DriveSearch.MaxExpansions, Allocator.Temp);
            var points = new NativeList<PathWaypoint>(64, Allocator.Temp);
            var start = Truck.Chassis.Pose(new float2(28f, 0f), new float2(-1f, 0f));

            var found = search.Run(start, DriveGoal.ToPose(new float2(10f, 0f), new float2(1f, 0f)), 0f, points);

            Assert.IsTrue(found, "found");
            var before = points[points.Length - 4].Position.xz;
            Assert.Less(before.x, 10f, "it comes in from the west");
            Assert.IsFalse(points[points.Length - 1].Reverse, "driving forwards");
            points.Dispose();
            search.Dispose();
        }
    }
}
