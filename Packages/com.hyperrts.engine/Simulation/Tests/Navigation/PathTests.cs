using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>
    /// Paths round walls, out of dead ends and through narrow gaps by width, keep their wall margin, re-plan when
    /// shoved, and track the length left past each corner.
    /// </summary>
    public partial class PathTests
    {
        private TestWorld _world;

        [TearDown]
        public void TearDown() => _world.Dispose();

        private void CreateMatch(float size)
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, size);
        }

        private DynamicBuffer<PathWaypoint> Waypoints(Entity unit) =>
            _world.EntityManager.GetBuffer<PathWaypoint>(unit);

        [Test]
        public void CrossMapPath_OnAFullSizeMap_EndsAtTheGoal()
        {
            CreateMatch(600f);
            // A wall down the middle with its only way round at the north end, among rotated buildings.
            _world.SpawnObstacle(new float3(0f, 0f, -25f), new float2(2f, 550f));
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
                    _world.SpawnObstacle(center, size, random.NextFloat(math.PI));
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
            _world.SpawnObstacle(new float3(0f, 0f, 10f), new float2(22f, 2f));
            _world.SpawnObstacle(new float3(-10f, 0f, 0f), new float2(2f, 20f));
            _world.SpawnObstacle(new float3(10f, 0f, 0f), new float2(2f, 20f));
            _world.Tick();
            var unit = _world.SpawnUnit(1, float3.zero);
            var goal = new float3(0f, 0f, 30f);
            _world.MoveTo(unit, goal);
            _world.Tick();

            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<MoveDestination>(unit), 40f), "the move ended");
            Assert.AreNotEqual(ArrivalReason.Stalled, _world.Get<Arrival>(unit).Reason, "by arriving");
            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 1f, "at the goal");
        }

        [Test]
        public void PathCorners_KeepTheWallMargin_InTheOpen()
        {
            CreateMatch(200f);
            _world.SpawnObstacle(float3.zero, new float2(20f, 20f));
            _world.SpawnObstacle(new float3(30f, 0f, 25f), new float2(12f, 12f), math.PI * 0.25f);
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
                    _world.SpawnObstacle(center, random.NextFloat2(new float2(6f), new float2(9f)),
                        random.NextFloat(math.PI));
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

        [Test]
        public void Path_RoutesAroundWallOfBuildings_NeverEnteringBlockedCells()
        {
            CreateMatch(200f);
            for (var z = -8f; z <= 8f; z += 4f)
            {
                _world.SpawnBuilding(1, new float3(0f, 0f, z), new float2(4f, 4f));
            }

            var unit = _world.SpawnUnit(1, new float3(-8f, 0f, 0f));
            var goal = new float3(8f, 0f, 0f);
            _world.MoveTo(unit, goal);

            var widest = 0f;
            for (var t = 0f; t < 12f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var position = _world.GetPosition(unit);
                Assert.IsTrue(_world.Grid().IsWalkable(position), $"entered a blocked cell at {position}");
                widest = math.max(widest, math.abs(position.z));
            }

            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 0.2f);
            Assert.Greater(widest, 10f, "the unit went around the wall's end");
        }

        [Test]
        public void WideUnit_TakesTheWideGap_WhileASmallUnitSlipsThroughTheNarrowOne()
        {
            CreateMatch(200f);
            _world.AddAgentPrefabs(new NavAgent { Radius = 0.5f }, new NavAgent { Radius = 1f });
            // A wall along Z with a one-cell gap at z 0 to 1 and a three-cell gap at z 10 to 13.
            _world.SpawnObstacle(new float3(0.5f, 0f, -10f), new float2(1f, 20f));
            _world.SpawnObstacle(new float3(0.5f, 0f, 5.5f), new float2(1f, 9f));
            _world.SpawnObstacle(new float3(0.5f, 0f, 22f), new float2(1f, 18f));
            _world.Tick();

            var small = _world.SpawnUnit(1, new float3(-6f, 0f, 0.5f), radius: 0.4f);
            var wide = _world.SpawnUnit(1, new float3(-6f, 0f, 5f), radius: 1f);
            var goal = new float3(6f, 0f, 0.5f);
            var wideGoal = new float3(6f, 0f, 5f);
            _world.MoveTo(small, goal);
            _world.MoveTo(wide, wideGoal);

            var smallWidest = 0f;
            var wideWidest = 0f;
            for (var t = 0f; t < 15f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                smallWidest = math.max(smallWidest, _world.GetPosition(small).z);
                wideWidest = math.max(wideWidest, _world.GetPosition(wide).z);
                Assert.IsTrue(_world.Grid().IsWalkable(_world.GetPosition(wide)), "the wide unit stays off the wall");
            }

            Assert.Less(smallWidest, 3f, "the small unit took the narrow gap");
            Assert.Greater(wideWidest, 10f, "the wide unit went round by the wide gap, though the narrow one is shorter");
            Assert.Less(math.distance(_world.GetPosition(wide).xz, wideGoal.xz), 1f);
            Assert.Less(math.distance(_world.GetPosition(small).xz, goal.xz), 1f);
        }

        [Test]
        public void ShovedBehindAPillar_StillArrives()
        {
            CreateMatch(200f);
            // A wall from z -6 to 14 whose south end is the way round, and a pillar north of the start.
            _world.SpawnObstacle(new float3(0.5f, 0f, 4f), new float2(1f, 20f));
            _world.SpawnObstacle(new float3(-2f, 0f, 10.5f), new float2(2f, 1f));
            _world.Tick();

            var unit = _world.SpawnUnit(1, new float3(-5f, 0f, 0f));
            var goal = new float3(5f, 0f, 0f);
            _world.MoveTo(unit, goal);
            _world.Run(0.3f);
            Assert.Less(Waypoints(unit)[0].Position.z, -6f, "the path rounds the wall's south end");

            // From here the north end is far shorter, so a fresh search would turn the unit round.
            var transform = _world.Get<LocalTransform>(unit);
            transform.Position = new float3(-2f, 0f, 12f);
            _world.EntityManager.SetComponentData(unit, transform);

            var arrived = _world.RunUntil(() => !_world.IsEnabled<MoveDestination>(unit), 20f);
            Assert.IsTrue(arrived, "the move ended");
            Assert.AreNotEqual(ArrivalReason.Stalled, _world.Get<Arrival>(unit).Reason,
                "by arriving, not by giving up");
            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 1f, "at the goal");
        }

        [Test]
        public void LengthPastCorner_StaysTheSum_AsCornersPassAndTheGoalMoves()
        {
            CreateMatch(200f);
            var waypoints = _world.EntityManager.AddBuffer<PathWaypoint>(_world.EntityManager.CreateEntity());
            foreach (var corner in new[] { new float2(0f, 0f), new float2(3f, 0f), new float2(3f, 4f) })
            {
                waypoints.Add(new PathWaypoint { Position = new float3(corner.x, 0f, corner.y) });
            }

            var path = new PathState { LengthPastCorner = PathState.Measure(waypoints) };
            Assert.AreEqual(7f, path.LengthPastCorner, 1e-4f);

            path.MoveLast(waypoints, new float2(3f, 6f));
            Assert.AreEqual(PathState.Measure(waypoints), path.LengthPastCorner, 1e-4f, "goal nudged");

            path.PassCorner(waypoints);
            Assert.AreEqual(6f, path.LengthPastCorner, 1e-4f, "first corner passed");

            path.ClearWaypoints(waypoints);
            Assert.AreEqual(0f, path.LengthPastCorner);
        }
    }
}
