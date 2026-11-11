using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Sectors;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation.Paths
{
    /// <summary>
    /// Group flow fields: a trace is as short as A*, a shoved member re-traces with no search, a building on the
    /// corridor rebuilds the field, and each movement class of a group gets its own.
    /// </summary>
    public class FlowFieldTests : SimulationFixture
    {
        private const int Group = 7;

        [SetUp]
        public void Prepare()
        {
            // A wall between the start and goal areas, open at both ends.
            World.SpawnObstacle(new float3(0f, 0f, 0f), new float2(4f, 120f), 0.2f);
            World.SpawnObstacle(new float3(-30f, 0f, 40f), new float2(16f, 10f), 0.7f);
        }

        private PathfindingSystem Pathfinder()
        {
            World.EntityManager.CompleteAllTrackedJobs();
            var handle = World.World.GetExistingSystem<PathfindingSystem>();
            return World.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle);
        }

        // A block of units west of the wall, each sent to its own slot east of it.
        private Entity[] OrderGroup(int count, float radius, float slotOffset = 0f)
        {
            var units = new Entity[count];
            for (var i = 0; i < count; i++)
            {
                var start = new float3(-40f + i % 4 * 4f, 0f, -20f + i / 4 * 4f + slotOffset);
                units[i] = World.SpawnUnit(1, start, new TestUnit { Radius = radius });
            }

            World.Tick();
            for (var i = 0; i < count; i++)
            {
                var slot = new float3(40f + i % 4 * 5f, 0f, -10f + i / 4 * 5f + slotOffset);
                World.Issue(units[i], new Order { Type = OrderType.Move, Position = slot, Group = Group });
            }

            World.Tick(frames: 2);
            return units;
        }

        [Test]
        public void TracedPath_IsWithinTwoPercentOfTheAStarPath()
        {
            var unit = World.SpawnUnit(1, new float3(-40f, 0f, -20f));
            World.Tick();
            var agent = World.Get<NavAgent>(unit);
            var grid = World.Grid();
            var access = agent.Access(grid);
            var start = new float3(-40f, 0f, -20f);
            var goal = new float3(45f, 0f, 30f);

            var points = new NativeList<float3>(Allocator.Temp);
            GridPathfinder.Plan(access, start, goal, points, out _);
            var searched = Length(start, points);

            var query = GridPathfinder.Prepare(access, start, goal);
            var sectors = new NativeList<int>(Allocator.Temp);
            GridPathfinder.FindSectors(access, query, sectors);
            var corridor = new NavCorridor(grid.Field.Layout, Allocator.Persistent);
            corridor.AddWithRings(sectors.AsArray());

            var field = new GroupField
            {
                Key = new int2(Group, access.ClassIndex), Field = new FlowField(corridor, query.To, Allocator.Persistent),
                Anchor = grid.Grid.CenterPoint(query.To), WallMargin = agent.WallMargin,
            };
            field.Field.Build(access);
            Assert.IsTrue(field.Trace(grid, query, points), "the start is in the field's reach");
            var traced = Length(start, points);
            field.Field.Dispose();
            sectors.Dispose();
            points.Dispose();

            Assert.Greater(searched, 100f, "round the wall");
            Assert.AreEqual(searched, traced, searched * 0.02f, "as short as the search");
        }

        private static float Length(float3 start, NativeList<float3> points)
        {
            var length = 0f;
            var from = start.xz;
            foreach (var point in points)
            {
                length += math.distance(from, point.xz);
                from = point.xz;
            }

            return length;
        }

        [Test]
        public void GroupOrder_BuildsOneField_AndShovedMemberReTracesWithoutASearch()
        {
            var units = OrderGroup(8, 0.5f);
            var before = Pathfinder().Stats;
            Assert.AreEqual(1, before.Fields, "one field for the group");
            Assert.AreEqual(8, before.Traces, "every member traced it");
            Assert.AreEqual(0, before.Searches, "no member searched");

            // Shoved aside, and asking for a replan as MoveProgress does.
            var shoved = units[2];
            var transform = World.Get<LocalTransform>(shoved);
            transform.Position += new float3(0f, 0f, 3f);
            World.Set(shoved, transform);
            var path = World.Get<PathState>(shoved);
            path.Status = PathStatus.None;
            World.Set(shoved, path);
            World.Tick();

            var after = Pathfinder().Stats;
            Assert.AreEqual(PathStatus.Ready, World.Get<PathState>(shoved).Status, "re-planned");
            Assert.AreEqual(before.Traces + 1, after.Traces, "by tracing the field again");
            Assert.AreEqual(before.Searches, after.Searches, "with no search");
            Assert.AreEqual(before.Fields, after.Fields, "nor a new field");
        }

        [Test]
        public void BuildingPlacedOnTheCorridor_RebuildsTheField()
        {
            var units = OrderGroup(8, 0.5f);
            var built = Pathfinder().Stats.Fields;

            // Across the members' way from the wall's south end to their slots.
            World.SpawnObstacle(new float3(14f, 0f, -36f), new float2(4f, 24f), 0f);
            World.Tick(frames: 2);

            var pathfinder = Pathfinder();
            Assert.Greater(pathfinder.Stats.Fields, built, "the field was built again");
            Assert.AreEqual(1, pathfinder.Fields.Fields.Length, "and the old one dropped");
            var access = World.Get<NavAgent>(units[0]).Access(World.Grid());
            foreach (var unit in units)
            {
                var from = World.GetPosition(unit);
                foreach (var waypoint in World.Buffer<PathWaypoint>(unit))
                {
                    Assert.IsTrue(access.HasLineOfSight(from, waypoint.Position), "each leg keeps to open ground");
                    from = waypoint.Position;
                }
            }
        }

        [Test]
        public void MixedGroup_GetsOneFieldPerClass()
        {
            var infantry = OrderGroup(4, 0.5f);
            var tanks = new Entity[3];
            for (var i = 0; i < tanks.Length; i++)
            {
                tanks[i] = World.SpawnUnit(1, new float3(-40f + i * 6f, 0f, 10f), new TestUnit { Radius = 1.85f });
            }

            World.Tick();
            for (var i = 0; i < tanks.Length; i++)
            {
                var slot = new float3(40f + i * 6f, 0f, 20f);
                World.Issue(tanks[i], new Order { Type = OrderType.Move, Position = slot, Group = Group });
            }

            World.Tick(frames: 2);
            var fields = Pathfinder().Fields.Fields;
            Assert.AreEqual(2, fields.Length, "infantry and tanks each have a field");
            Assert.AreNotEqual(fields[0].Key.y, fields[1].Key.y, "of their own class");
            Assert.AreEqual(fields[0].Key.x, fields[1].Key.x, "for the one group");
            Assert.AreEqual(PathStatus.Ready, World.Get<PathState>(infantry[0]).Status);
            Assert.AreEqual(PathStatus.Ready, World.Get<PathState>(tanks[0]).Status);
        }
    }
}
