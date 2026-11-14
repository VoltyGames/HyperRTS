using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Navigation.Agents;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Air
{
    /// <summary>Flight handling: aircraft speed up, brake into their goal and turn their nose no faster than they may.</summary>
    public class FlightTests : SimulationFixture
    {
        // Gunship handling: slow to speed up, banks 30 degrees, noses round at 60 degrees a second.
        private static readonly Flight Gunship = new()
        {
            Altitude = 10f,
            ClimbSpeed = 6f,
            Acceleration = 3f,
            Braking = 4f,
            MaxBank = math.radians(30f),
            YawRate = math.radians(60f),
        };

        [Test]
        public void Aircraft_SpeedsUpOverTime_AndBrakesToAStopAtItsGoal()
        {
            var aircraft = Spawn();
            var goal = new float3(0f, 0f, 60f);

            World.MoveTo(aircraft, goal);
            World.Run(1f);
            Assert.AreEqual(Gunship.Acceleration, Speed(aircraft), 0.5f, "a second in, it has gained one second of speed");

            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<MoveDestination>(aircraft), 20f), "arrives");
            World.Run(1f);
            Assert.Less(Speed(aircraft), 0.1f, "stops");
            Assert.Less(math.distance(World.GetPosition(aircraft).xz, goal.xz), 1f, "braked into its goal, not past it");
        }

        [Test]
        public void Aircraft_OrderedAboutAtSpeed_SwingsRoundNoFasterThanItsYawRate()
        {
            var aircraft = Spawn();
            World.MoveTo(aircraft, new float3(0f, 0f, 200f));
            World.Run(4f);
            var behind = new float3(0f, 0f, -40f);
            World.MoveTo(aircraft, behind);

            var facing = Facing(aircraft);
            var fastest = 0f;
            for (var t = 0f; t < 3f; t += TestWorld.FrameTime)
            {
                World.Tick();
                var now = Facing(aircraft);
                fastest = math.max(fastest, math.abs(Planar.SignedAngle(facing, now)));
                facing = now;
            }

            Assert.LessOrEqual(fastest, Gunship.YawRate * TestWorld.FrameTime + 1e-4f, "the nose turns at its yaw rate");
            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<MoveDestination>(aircraft), 30f), "comes round and arrives");
            Assert.Less(math.distance(World.GetPosition(aircraft).xz, behind.xz), 1f);
        }

        [Test]
        public void Aircraft_WhoseGoalAnEnemyHoversOn_GivesUpBesideIt()
        {
            var goal = new float3(0f, 0f, 30f);
            World.SpawnAircraft(2, goal);
            var aircraft = Spawn();

            World.MoveTo(aircraft, goal);

            Assert.IsTrue(World.RunUntil(() => World.IsEnabled<Arrival>(aircraft), 30f), "the move ends");
            Assert.AreEqual(ArrivalReason.Stalled, World.Get<Arrival>(aircraft).Reason);
            Assert.Less(math.distance(World.GetPosition(aircraft).xz, goal.xz), 2f, "beside the aircraft on its goal");
        }

        private Entity Spawn()
        {
            var aircraft = World.SpawnAircraft(1, float3.zero, Gunship.Altitude, speed: 9f);
            World.Set(aircraft, Gunship);
            return aircraft;
        }

        private float Speed(Entity aircraft) => math.length(World.Get<FlightState>(aircraft).Velocity);

        private float2 Facing(Entity aircraft) => math.normalize(World.Get<LocalTransform>(aircraft).Forward().xz);
    }
}
