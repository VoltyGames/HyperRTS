using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Paths reach the goal across a full-size map, lead out of dead ends and keep their wall margin.</summary>
    public class PathQualityTests
    {
        private TestWorld _world;

        [TearDown]
        public void TearDown() => _world.Dispose();

        private void CreateMatch(float size)
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, size);
        }

        private void SpawnObstacle(float3 center, float2 size, float yaw = 0f)
        {
            var obstacle = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(obstacle,
                LocalTransform.FromPositionRotation(center, quaternion.RotateY(yaw)));
            var writer = new EntityManagerWriter(_world.EntityManager, obstacle);
            NavSetup.AddObstacle(ref writer, size);
        }

        private DynamicBuffer<PathWaypoint> Waypoints(Entity unit) =>
            _world.EntityManager.GetBuffer<PathWaypoint>(unit);

        [Test]
        public void CrossMapPath_OnAFullSizeMap_EndsAtTheGoal()
        {
            CreateMatch(600f);
            // A wall down the middle with its only way round at the north end, among rotated buildings.
            SpawnObstacle(new float3(0f, 0f, -25f), new float2(2f, 550f));
            var start = new float3(-270f, 0f, -270f);
            var goal = new float3(270f, 0f, -270f);
            var random = new Random(3);
            for (var i = 0; i < 40; i++)
            {
                var center = new float3(random.NextFloat(-280f, 280f), 0f, random.NextFloat(-280f, 230f));
                var clear = math.abs(center.x) > 20f && math.distance(center, start) > 40f &&
                            math.distance(center, goal) > 40f;
                if (clear)
                {
                    var size = random.NextFloat2(new float2(8f), new float2(30f));
                    SpawnObstacle(center, size, random.NextFloat(math.PI));
                }
            }

            _world.Tick();
            var unit = _world.SpawnUnit(1, start);
            _world.MoveTo(unit, goal);
            _world.Tick(frames: 2);

            var path = _world.Get<PathState>(unit);
            Assert.AreEqual(PathStatus.Ready, path.Status);
            Assert.IsFalse(path.IsShort, "the goal wasn't snapped");
            var waypoints = Waypoints(unit);
            Assert.Less(math.distance(waypoints[waypoints.Length - 1].Position.xz, goal.xz), 0.01f, "ends at the goal");
            var length = math.distance(start.xz, waypoints[0].Position.xz) + path.LengthPastCorner;
            Assert.Greater(length, 1000f, "by way of the wall's north end");
        }

        [Test]
        public void UnitInAUTrap_LeadsOutAndRoundToTheGoal()
        {
            CreateMatch(200f);
            // A cup open to the south, its bottom between the unit and the goal.
            SpawnObstacle(new float3(0f, 0f, 10f), new float2(22f, 2f));
            SpawnObstacle(new float3(-10f, 0f, 0f), new float2(2f, 20f));
            SpawnObstacle(new float3(10f, 0f, 0f), new float2(2f, 20f));
            _world.Tick();
            var unit = _world.SpawnUnit(1, float3.zero);
            var goal = new float3(0f, 0f, 30f);
            _world.MoveTo(unit, goal);
            _world.Tick();

            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<MoveDestination>(unit), 40f), "the move ended");
            Assert.IsFalse(_world.Get<Arrival>(unit).Stalled, "by arriving");
            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 1f, "at the goal");
        }

        [Test]
        public void PathCorners_KeepTheWallMargin_InTheOpen()
        {
            CreateMatch(200f);
            SpawnObstacle(float3.zero, new float2(20f, 20f));
            SpawnObstacle(new float3(30f, 0f, 25f), new float2(12f, 12f), math.PI * 0.25f);
            _world.Tick();
            var unit = _world.SpawnUnit(1, new float3(-30f, 0f, -5f));
            var goal = new float3(45f, 0f, 45f);
            _world.MoveTo(unit, goal);
            _world.Tick(frames: 2);

            var agent = _world.Get<NavAgent>(unit);
            var access = agent.Access(_world.Grid());
            var waypoints = Waypoints(unit);
            Assert.Greater(waypoints.Length, 1, "it turns round the buildings");
            for (var i = 0; i < waypoints.Length - 1; i++)
            {
                var clearance = access.Distance(waypoints[i].Position.xz);
                Assert.GreaterOrEqual(clearance, agent.WallMargin - 0.5f, $"corner {i} at {waypoints[i].Position}");
            }
        }
        [Test]
        public void DenseBaseOfRotatedBuildings_PathThreadsTheGaps_AndArrives()
        {
            CreateMatch(200f);
            var random = new Random(21);
            for (var x = 0; x < 6; x++)
            {
                for (var z = 0; z < 6; z++)
                {
                    var center = new float3(-35f + x * 14f, 0f, -35f + z * 14f);
                    SpawnObstacle(center, random.NextFloat2(new float2(6f), new float2(9f)), random.NextFloat(math.PI));
                }
            }

            _world.Tick();
            var start = new float3(-70f, 0f, -60f);
            var goal = new float3(0f, 0f, 0f);
            var unit = _world.SpawnUnit(1, start);
            _world.MoveTo(unit, goal);
            _world.Tick(frames: 2);

            var path = _world.Get<PathState>(unit);
            Assert.AreEqual(PathStatus.Ready, path.Status);
            Assert.IsFalse(path.IsShort, "the gap between buildings is reachable");
            AssertLegsInSight(unit, start);
            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<MoveDestination>(unit), 40f), "the move ended");
            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 1f, "at the goal inside the base");
        }

        [Test]
        public void RiverWithOneBridge_PathCrossesOnTheBridge()
        {
            CreateMatch(200f);
            _world.SpawnNavArea(new float3(0f, 0f, 10f), new float2(200f, 12f), NavAreaKind.Water);
            _world.SpawnNavArea(new float3(60f, 0f, 10f), new float2(8f, 14f), NavAreaKind.Deck);
            _world.Tick();
            var start = new float3(-60f, 0f, -20f);
            var goal = new float3(-60f, 0f, 40f);
            var unit = _world.SpawnUnit(1, start);
            _world.MoveTo(unit, goal);
            _world.Tick(frames: 2);

            var path = _world.Get<PathState>(unit);
            Assert.AreEqual(PathStatus.Ready, path.Status);
            Assert.IsFalse(path.IsShort, "the far bank is reachable over the bridge");
            AssertLegsInSight(unit, start);
            var crosses = false;
            foreach (var waypoint in Waypoints(unit))
            {
                crosses |= math.abs(waypoint.Position.x - 60f) < 6f;
            }

            Assert.IsTrue(crosses, "a corner at the bridge");
        }

        // Every leg of the path keeps to open ground.
        private void AssertLegsInSight(Entity unit, float3 start)
        {
            var access = _world.Get<NavAgent>(unit).Access(_world.Grid());
            var from = start;
            foreach (var waypoint in Waypoints(unit))
            {
                Assert.IsTrue(access.HasLineOfSight(from, waypoint.Position), $"leg {from} to {waypoint.Position}");
                from = waypoint.Position;
            }
        }
    }
}
