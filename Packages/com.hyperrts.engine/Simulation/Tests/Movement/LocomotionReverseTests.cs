using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using static HyperRTS.Simulation.Tests.TestVehicles;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Reversing, backing out and three-point turns.</summary>
    public class LocomotionReverseTests : LocomotionFixture
    {
        // Seconds of driving forwards a reversed route may take, for one manoeuvre where a reverse curve can't fit.
        private const float OneManoeuvre = 3f;

        private struct Backing
        {
            public float Forwards;
            public float Fastest;
        }

        private void ReverseOrder(Entity unit, float3 goal, bool queue = false) =>
            World.Command(World.Get<Owner>(unit).Value, new PlayerCommand { Type = CommandType.ReverseMove, Unit = unit, Position = goal, Queue = queue });

        // The way west is shut, so a route to (30, -30) runs south, then east round the block's corner.
        private void SpawnCornerBlock()
        {
            World.SpawnObstacle(new float3(22f, 0f, -7f), new float2(36f, 34f));
            World.Tick();
        }

        private Backing BackUntilArrived(Entity unit, float cap)
        {
            var backing = new Backing();
            World.Tick();
            for (var t = 0f; t < cap && !Arrived(unit); t += TestWorld.FrameTime)
            {
                World.Tick();
                var speed = Speed(unit);
                backing.Forwards += speed > 0.1f ? TestWorld.FrameTime : 0f;
                backing.Fastest = math.min(backing.Fastest, speed);
            }

            Assert.IsTrue(Arrived(unit), $"arrived: stands at {World.GetPosition(unit).xz}, facing {Facing(unit)}");
            return backing;
        }

        private void AssertBackedRoundTheCorner(Entity unit, float radius)
        {
            var backing = BackUntilArrived(unit, 90f);
            Assert.Less(backing.Forwards, OneManoeuvre, "drove backwards");
            Assert.GreaterOrEqual(backing.Fastest, -Tracks.ReverseSpeed - 0.01f, "no faster than its reverse speed");
            Assert.Less(math.dot(Facing(unit), new float2(1f, 0f)), -0.8f, "faces away from the way it drove");
            Assert.Less(math.distance(World.GetPosition(unit).xz, new float2(30f, -30f)), radius + 1f);
        }

        [Test]
        public void ReverseMove_Truck_BacksRoundACorner_AndArrivesFacingAway()
        {
            SpawnCornerBlock();
            var truck = SpawnTruck(float3.zero);
            ReverseOrder(truck, new float3(30f, 0f, -30f));
            AssertBackedRoundTheCorner(truck, TruckRadius);
        }

        [Test]
        public void ReverseMove_Tank_BacksRoundACorner_AndArrivesFacingAway()
        {
            SpawnCornerBlock();
            var tank = SpawnTank(float3.zero);
            ReverseOrder(tank, new float3(30f, 0f, -30f));
            AssertBackedRoundTheCorner(tank, TankRadius);
        }

        [Test]
        public void ReverseMove_QueuedAfterAMove_DrivesForwardThenBacks()
        {
            var tank = SpawnTank(float3.zero);
            Order(tank, new float3(0f, 0f, 20f));
            ReverseOrder(tank, new float3(0f, 0f, 60f), queue: true);

            Assert.IsTrue(World.RunUntil(() => World.GetPosition(tank).z > 15f, 20f), "drove the move first");
            Assert.Greater(Speed(tank), 0f, "forwards");
            Assert.IsTrue(World.RunUntil(() => World.GetPosition(tank).z > 58f, 60f), "then the reverse move");
            Assert.Less(math.dot(Facing(tank), new float2(0f, 1f)), -0.9f, "backed there");
        }

        [Test]
        public void ReverseMove_Infantry_WalkThereAsOnAMove()
        {
            var soldier = World.SpawnUnit(1, float3.zero);
            ReverseOrder(soldier, new float3(0f, 0f, -20f));
            World.Tick();

            Assert.IsTrue(World.RunUntil(() => Arrived(soldier), 10f), "arrived");
            Assert.Greater(math.dot(Facing(soldier), new float2(0f, -1f)), 0.9f, "faced its way");
        }

        [Test]
        public void Truck_BacksUpAShortWayBehind_InALane_WithoutTurningRound()
        {
            World.SpawnObstacle(new float3(-8f, 0f, -10f), new float2(4f, 40f));
            World.SpawnObstacle(new float3(8f, 0f, -10f), new float2(4f, 40f));
            World.Tick();
            var truck = SpawnTruck(float3.zero);
            var goal = new float3(0f, 0f, -15f);
            Order(truck, goal);

            var backing = BackUntilArrived(truck, 30f);
            Assert.Less(backing.Fastest, -1f, "reversed");
            Assert.Greater(math.dot(Facing(truck), new float2(0f, 1f)), 0.95f, "never turned round");
            Assert.Less(math.distance(World.GetPosition(truck).xz, goal.xz), NavTolerances.Arrive(TruckRadius) + 0.1f);
        }
    }
}
