using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>A unit a building lands on drives out by itself, and one pinned in walls gives up its move.</summary>
    public class SunkUnitTests : SimulationFixture
    {
        private bool IsSunk(Entity unit)
        {
            var agent = World.Get<NavAgent>(unit);
            var capsule = new NavCapsule(agent.Access(World.Grid()), float2.zero);
            return capsule.Depth(World.GetPosition(unit).xz, float2.zero) > NavTolerances.WallOverlap;
        }

        [Test]
        public void ParkedUnit_UnderANewObstacle_DrivesOutByTheNearestEdge()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            World.Tick();
            // Its south edge is 1.5 m away, the others 2.5 m or more.
            World.SpawnObstacle(new float3(0f, 0f, 1f), new float2(8f, 5f));
            World.Tick();
            Assert.IsTrue(IsSunk(unit), "the obstacle landed on it");

            World.Run(2f);

            Assert.IsFalse(IsSunk(unit), "drove out");
            var position = World.GetPosition(unit);
            Assert.Less(position.z, -1.5f, "by the south edge");
            Assert.AreEqual(0f, position.x, 0.3f, "straight out");
            Assert.IsFalse(World.IsEnabled<MoveDestination>(unit), "still parked");
        }

        [Test]
        public void MovingUnit_DeepInAWideBuilding_DrivesOutAndArrives()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            World.Tick();
            // Deeper than the field's cap, where it reads flat.
            World.SpawnObstacle(float3.zero, new float2(24f, 24f));
            World.Tick();
            var goal = new float3(0f, 0f, 20f);
            World.MoveTo(unit, goal);
            World.Tick();

            var ended = World.RunUntil(() => !World.IsEnabled<MoveDestination>(unit), 20f);

            Assert.IsTrue(ended, "the move ended");
            Assert.AreNotEqual(ArrivalReason.Stalled, World.Get<Arrival>(unit).Reason,
                "by arriving, not by giving up");
            Assert.Less(math.distance(World.GetPosition(unit).xz, goal.xz), 1f, "at the goal");
        }

        [Test]
        public void PinnedUnit_GivesUpItsMove_InsteadOfReplanningForever()
        {
            // A sealed two-metre slot, too narrow for a three-metre unit to drive along or leave.
            var unit = World.SpawnUnit(1, float3.zero, radius: 1.5f);
            World.Tick();
            World.SpawnObstacle(new float3(-3f, 0f, 0f), new float2(4f, 40f));
            World.SpawnObstacle(new float3(3f, 0f, 0f), new float2(4f, 40f));
            World.SpawnObstacle(new float3(0f, 0f, 21f), new float2(10f, 2f));
            World.SpawnObstacle(new float3(0f, 0f, -21f), new float2(10f, 2f));
            World.Tick();
            World.MoveTo(unit, new float3(30f, 0f, 0f));

            World.Run(MoveProgress.GiveUpSeconds + 2f);

            Assert.IsFalse(World.IsEnabled<MoveDestination>(unit), "the move ended");
            Assert.IsTrue(World.IsEnabled<Arrival>(unit) &&
                World.Get<Arrival>(unit).Reason == ArrivalReason.Stalled, "it gave up");
        }
    }
}
