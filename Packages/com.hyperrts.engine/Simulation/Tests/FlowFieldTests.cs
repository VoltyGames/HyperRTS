using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// Group flow fields: a trace is as short as A*, a shoved member re-traces with no search, a building on the
    /// corridor rebuilds the field, and each movement class of a group gets its own.
    /// </summary>
    public class FlowFieldTests
    {
        private const int Group = 7;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            // A wall between the start and goal areas, open at both ends.
            _world.SpawnObstacle(new float3(0f, 0f, 0f), new float2(4f, 120f), 0.2f);
            _world.SpawnObstacle(new float3(-30f, 0f, 40f), new float2(16f, 10f), 0.7f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private PathfindingSystem Pathfinder()
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            var handle = _world.World.GetExistingSystem<PathfindingSystem>();
            return _world.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle);
        }

        // A block of units west of the wall, each sent to its own slot east of it.
        private Entity[] OrderGroup(int count, float radius, float slotOffset = 0f)
        {
            var units = new Entity[count];
            for (var i = 0; i < count; i++)
            {
                var start = new float3(-40f + i % 4 * 4f, 0f, -20f + i / 4 * 4f + slotOffset);
                units[i] = _world.SpawnUnit(1, start, radius: radius);
            }

            _world.Tick();
            for (var i = 0; i < count; i++)
            {
                var slot = new float3(40f + i % 4 * 5f, 0f, -10f + i / 4 * 5f + slotOffset);
                _world.Issue(units[i], new Order { Type = OrderType.Move, Position = slot, Group = Group });
            }

            _world.Tick(frames: 2);
            return units;
        }

        [Test]
        public void TracedPath_IsWithinTwoPercentOfTheAStarPath()
        {
            var unit = _world.SpawnUnit(1, new float3(-40f, 0f, -20f));
            _world.Tick();
            var agent = _world.Get<NavAgent>(unit);
            var grid = _world.Grid();
            var access = agent.Access(grid);
            var start = new float3(-40f, 0f, -20f);
            var goal = new float3(45f, 0f, 30f);

            var points = new NativeList<float3>(Allocator.Temp);
            GridPathfinder.Plan(access, start, goal, points, out _);
            var searched = Length(start, points);

            var query = GridPathfinder.Prepare(access, start, goal);
            var sectors = new NativeList<int>(Allocator.Temp);
            GridPathfinder.FindSectors(access, query, sectors);
            var corridor = new NavCorridor(grid.Field.SectorsPerAxis, Allocator.Persistent);
            foreach (var sector in sectors)
            {
                corridor.AddWithRing(sector);
            }

            var field = new GroupField
            {
                Key = new int2(Group, access.ClassIndex), Field = new FlowField(corridor, query.To, Allocator.Persistent),
                Anchor = grid.CellCenter(query.To), WallMargin = agent.WallMargin,
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
            var transform = _world.Get<LocalTransform>(shoved);
            transform.Position += new float3(0f, 0f, 3f);
            _world.EntityManager.SetComponentData(shoved, transform);
            var path = _world.Get<PathState>(shoved);
            path.Status = PathStatus.None;
            _world.EntityManager.SetComponentData(shoved, path);
            _world.Tick();

            var after = Pathfinder().Stats;
            Assert.AreEqual(PathStatus.Ready, _world.Get<PathState>(shoved).Status, "re-planned");
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
            _world.SpawnObstacle(new float3(14f, 0f, -36f), new float2(4f, 24f), 0f);
            _world.Tick(frames: 2);

            var pathfinder = Pathfinder();
            Assert.Greater(pathfinder.Stats.Fields, built, "the field was built again");
            Assert.AreEqual(1, pathfinder.Fields.Fields.Length, "and the old one dropped");
            var access = _world.Get<NavAgent>(units[0]).Access(_world.Grid());
            foreach (var unit in units)
            {
                var from = _world.GetPosition(unit);
                foreach (var waypoint in _world.EntityManager.GetBuffer<PathWaypoint>(unit))
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
                tanks[i] = _world.SpawnUnit(1, new float3(-40f + i * 6f, 0f, 10f), radius: 1.85f);
            }

            _world.Tick();
            for (var i = 0; i < tanks.Length; i++)
            {
                var slot = new float3(40f + i * 6f, 0f, 20f);
                _world.Issue(tanks[i], new Order { Type = OrderType.Move, Position = slot, Group = Group });
            }

            _world.Tick(frames: 2);
            var fields = Pathfinder().Fields.Fields;
            Assert.AreEqual(2, fields.Length, "infantry and tanks each have a field");
            Assert.AreNotEqual(fields[0].Key.y, fields[1].Key.y, "of their own class");
            Assert.AreEqual(fields[0].Key.x, fields[1].Key.x, "for the one group");
            Assert.AreEqual(PathStatus.Ready, _world.Get<PathState>(infantry[0]).Status);
            Assert.AreEqual(PathStatus.Ready, _world.Get<PathState>(tanks[0]).Status);
        }
    }
}
