using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>A chased goal re-aims the path's end by sight and searches again at most twice a second.</summary>
    public class PathChaseTests
    {
        private TestWorld _world;
        private Entity _unit;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            // A wall from x -20 to 20 between the unit and its target, so the path turns a corner.
            _world.SpawnObstacle(float3.zero, new float2(40f, 4f));
            _world.Tick();
            _unit = _world.SpawnUnit(1, new float3(0f, 0f, -30f));
            _world.Tick();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private void Chase(float3 goal)
        {
            _world.EntityManager.SetComponentData(_unit, new MoveDestination { Value = goal, Chase = true });
            _world.EntityManager.SetComponentEnabled<MoveDestination>(_unit, true);
        }

        private int Searches()
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            var handle = _world.World.GetExistingSystem<PathfindingSystem>();
            return _world.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle).Stats.Searches;
        }

        private float3 LastWaypoint()
        {
            var waypoints = _world.EntityManager.GetBuffer<PathWaypoint>(_unit);
            return waypoints[waypoints.Length - 1].Position;
        }

        private bool IsReady() => _world.Get<PathState>(_unit).Status == PathStatus.Ready;

        [Test]
        public void MovingTarget_InSight_ReAimsThePathWithoutASearch()
        {
            Chase(new float3(0f, 0f, 30f));
            Assert.IsTrue(_world.RunUntil(IsReady, 1f), "planned");
            var searches = Searches();
            Assert.Greater(_world.EntityManager.GetBuffer<PathWaypoint>(_unit).Length, 1, "round the wall");

            var goal = new float3(0f, 0f, 30f);
            for (var step = 0; step < 10; step++)
            {
                goal.x += 1.5f;
                Chase(goal);
                _world.Tick(TestWorld.FrameTime, 5);
            }

            Assert.AreEqual(searches, Searches(), "no new search");
            Assert.AreEqual(0f, math.distance(LastWaypoint().xz, goal.xz), 1e-3f, "the path ends at the target");
        }

        [Test]
        public void TargetJumpingFar_SearchesAtMostEveryHalfSecond()
        {
            Chase(new float3(0f, 0f, 30f));
            Assert.IsTrue(_world.RunUntil(IsReady, 1f), "planned");
            _world.Run(PathState.ChaseSearchSeconds);

            // Each jump moves the goal past a quarter of the path left, so it needs a search.
            var searches = Searches();
            Chase(new float3(-30f, 0f, 30f));
            _world.Tick(TestWorld.FrameTime, 2);
            Assert.AreEqual(searches + 1, Searches(), "an old path searches at once");

            Chase(new float3(30f, 0f, 30f));
            var held = 0f;
            for (; held < 1f && Searches() == searches + 1; held += TestWorld.FrameTime)
            {
                _world.Tick();
            }

            Assert.AreEqual(searches + 2, Searches(), "searched again");
            Assert.GreaterOrEqual(held, PathState.ChaseSearchSeconds - 2f * TestWorld.FrameTime, "but not sooner");
        }

        [Test]
        public void NewOrder_PlansAtOnce()
        {
            Chase(new float3(0f, 0f, 30f));
            Assert.IsTrue(_world.RunUntil(IsReady, 1f), "planned");
            var searches = Searches();

            _world.EntityManager.SetComponentData(_unit, new MoveDestination { Value = new float3(-30f, 0f, 30f) });
            _world.Tick(TestWorld.FrameTime, 2);

            Assert.AreEqual(searches + 1, Searches(), "an order isn't held back");
        }
    }
}
