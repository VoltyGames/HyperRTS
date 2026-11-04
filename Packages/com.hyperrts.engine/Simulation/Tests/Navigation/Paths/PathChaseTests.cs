using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Paths;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Navigation.Paths
{
    /// <summary>A chased goal re-aims the path's end by sight and searches again at most twice a second.</summary>
    public class PathChaseTests : SimulationFixture
    {
        private Entity _unit;

        [SetUp]
        public void Prepare()
        {
            // A wall from x -20 to 20 between the unit and its target, so the path turns a corner.
            World.SpawnObstacle(float3.zero, new float2(40f, 4f));
            World.Tick();
            _unit = World.SpawnUnit(1, new float3(0f, 0f, -30f));
            World.Tick();
        }

        private void Chase(float3 goal)
        {
            World.Set(_unit, new MoveDestination { Value = goal, Chase = true });
            World.SetEnabled<MoveDestination>(_unit, true);
        }

        private int Searches()
        {
            World.EntityManager.CompleteAllTrackedJobs();
            var handle = World.World.GetExistingSystem<PathfindingSystem>();
            return World.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle).Stats.Searches;
        }

        private float3 LastWaypoint()
        {
            var waypoints = World.Buffer<PathWaypoint>(_unit);
            return waypoints[waypoints.Length - 1].Position;
        }

        private bool IsReady() => World.Get<PathState>(_unit).Status == PathStatus.Ready;

        [Test]
        public void MovingTarget_InSight_ReAimsThePathWithoutASearch()
        {
            Chase(new float3(0f, 0f, 30f));
            Assert.IsTrue(World.RunUntil(IsReady, 1f), "planned");
            var searches = Searches();
            Assert.Greater(World.Buffer<PathWaypoint>(_unit).Length, 1, "round the wall");

            var goal = new float3(0f, 0f, 30f);
            for (var step = 0; step < 10; step++)
            {
                goal.x += 1.5f;
                Chase(goal);
                World.Tick(TestWorld.FrameTime, 5);
            }

            Assert.AreEqual(searches, Searches(), "no new search");
            Assert.AreEqual(0f, math.distance(LastWaypoint().xz, goal.xz), 1e-3f, "the path ends at the target");
        }

        [Test]
        public void TargetJumpingFar_SearchesAtMostEveryHalfSecond()
        {
            Chase(new float3(0f, 0f, 30f));
            Assert.IsTrue(World.RunUntil(IsReady, 1f), "planned");
            World.Run(PathState.ChaseSearchSeconds);

            // Each jump moves the goal past a quarter of the path left, so it needs a search.
            var searches = Searches();
            Chase(new float3(-30f, 0f, 30f));
            World.Tick(TestWorld.FrameTime, 2);
            Assert.AreEqual(searches + 1, Searches(), "an old path searches at once");

            Chase(new float3(30f, 0f, 30f));
            var held = 0f;
            for (; held < 1f && Searches() == searches + 1; held += TestWorld.FrameTime)
            {
                World.Tick();
            }

            Assert.AreEqual(searches + 2, Searches(), "searched again");
            Assert.GreaterOrEqual(held, PathState.ChaseSearchSeconds - 2f * TestWorld.FrameTime, "but not sooner");
        }

        [Test]
        public void NewOrder_PlansAtOnce()
        {
            Chase(new float3(0f, 0f, 30f));
            Assert.IsTrue(World.RunUntil(IsReady, 1f), "planned");
            var searches = Searches();

            World.Set(_unit, new MoveDestination { Value = new float3(-30f, 0f, 30f) });
            World.Tick(TestWorld.FrameTime, 2);

            Assert.AreEqual(searches + 1, Searches(), "an order isn't held back");
        }
    }
}
