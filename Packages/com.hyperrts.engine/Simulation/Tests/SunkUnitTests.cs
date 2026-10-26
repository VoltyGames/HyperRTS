using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>A unit a building lands on drives out by itself, and one pinned in walls gives up its move.</summary>
    public class SunkUnitTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private bool IsSunk(Entity unit)
        {
            var agent = _world.Get<NavAgent>(unit);
            var capsule = new NavCapsule(agent.Access(_world.Grid()), float2.zero);
            return capsule.Depth(_world.GetPosition(unit).xz, float2.zero) > NavTolerances.Skin;
        }

        [Test]
        public void ParkedUnit_UnderANewObstacle_DrivesOutByTheNearestEdge()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            _world.Tick();
            // Its south edge is 1.5 m away, the others 2.5 m or more.
            _world.SpawnObstacle(new float3(0f, 0f, 1f), new float2(8f, 5f));
            _world.Tick();
            Assert.IsTrue(IsSunk(unit), "the obstacle landed on it");

            _world.Run(2f);

            Assert.IsFalse(IsSunk(unit), "drove out");
            var position = _world.GetPosition(unit);
            Assert.Less(position.z, -1.5f, "by the south edge");
            Assert.AreEqual(0f, position.x, 0.3f, "straight out");
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(unit), "still parked");
        }

        [Test]
        public void MovingUnit_DeepInAWideBuilding_DrivesOutAndArrives()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            _world.Tick();
            // Deeper than the field's cap, where it reads flat.
            _world.SpawnObstacle(float3.zero, new float2(24f, 24f));
            _world.Tick();
            var goal = new float3(0f, 0f, 20f);
            _world.MoveTo(unit, goal);
            _world.Tick();

            var ended = _world.RunUntil(() => !_world.IsEnabled<MoveDestination>(unit), 20f);

            Assert.IsTrue(ended, "the move ended");
            Assert.AreNotEqual(ArrivalReason.Stalled, _world.Get<Arrival>(unit).Reason,
                "by arriving, not by giving up");
            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 1f, "at the goal");
        }

        [Test]
        public void PinnedUnit_GivesUpItsMove_InsteadOfReplanningForever()
        {
            // A two-metre slot between two walls, too narrow for a three-metre unit to leave either way.
            var unit = _world.SpawnUnit(1, float3.zero, radius: 1.5f);
            _world.Tick();
            _world.SpawnObstacle(new float3(-3f, 0f, 0f), new float2(4f, 40f));
            _world.SpawnObstacle(new float3(3f, 0f, 0f), new float2(4f, 40f));
            _world.Tick();
            _world.MoveTo(unit, new float3(30f, 0f, 0f));

            _world.Run(MoveProgress.GiveUpSeconds + 2f);

            Assert.IsFalse(_world.IsEnabled<MoveDestination>(unit), "the move ended");
            Assert.IsTrue(_world.IsEnabled<Arrival>(unit) &&
                _world.Get<Arrival>(unit).Reason == ArrivalReason.Stalled, "it gave up");
        }
    }
}
