using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Navigation.Steering
{
    public partial class LocomotionTests
    {
        // Slow enough that the turn radius, not grip, limits the steer.
        private const float Crawling = 3f;

        private struct Frame
        {
            public float2 Rear;
            public float2 Front;
            public float2 Facing;
            public float SteerAngle;
        }

        // Drives the truck alone toward a goal behind it on the left, which takes full lock.
        private static Frame[] DriveToFullLock(int frames)
        {
            var truck = Driving(Wheels, TruckChassis, TruckRadius, Crawling, new float2(0f, 1f));
            var goal = new float2(-20f, -20f);
            var log = new Frame[frames];
            for (var i = 0; i < frames; i++)
            {
                var leg = new PathLeg(truck.Position, goal, float2.zero, 0f, math.distance(truck.Position, goal), true);
                truck.Position += truck.Drive(leg);
                var pose = TruckChassis.Pose(truck.Position, truck.Facing);
                log[i] = new Frame
                {
                    Rear = pose.Position, Front = TruckChassis.FrontAxle(pose), Facing = truck.Facing,
                    SteerAngle = math.atan(TruckChassis.Wheelbase * truck.State.Steer),
                };
            }

            return log;
        }

        [Test]
        public void SteadyTurn_RearAxleCirclesAtTheTurnRadius_FrontAxleWider()
        {
            var log = DriveToFullLock(60);
            // Full lock is reached within a second at 45 degrees per second; the second second holds it.
            for (var i = 31; i < 60; i++)
            {
                var turn = TurnBetween(log[i - 1].Facing, log[i].Facing);
                Assert.AreEqual(TruckTurnRadius, math.distance(log[i - 1].Rear, log[i].Rear) / turn, 0.05f, "rear axle");
                var front = math.sqrt(TruckTurnRadius * TruckTurnRadius + 4.5f * 4.5f);
                Assert.AreEqual(front, math.distance(log[i - 1].Front, log[i].Front) / turn, 0.05f, "front axle");
            }
        }

        [Test]
        public void SteerAngle_ChangesNoFasterThanTheSteerRate()
        {
            var log = DriveToFullLock(40);
            var most = Wheels.SteerRate * TestWorld.FrameTime + 1e-5f;
            var last = 0f;
            foreach (var frame in log)
            {
                Assert.LessOrEqual(math.abs(frame.SteerAngle - last), most);
                last = frame.SteerAngle;
            }

            Assert.AreEqual(math.atan(4.5f / TruckTurnRadius), last, 1e-3f, "reaches full lock");
        }

        [Test]
        public void TrackedHull_PivotsAboutItsCentre()
        {
            var tank = Driving(Tracks, TrackedChassis, TankRadius, 6f, new float2(0f, 1f));
            var behind = new PathLeg(float2.zero, new float2(-5f, -30f), float2.zero, 0f, 30f, true);

            var step = tank.Drive(behind);

            MathAssert.Near(float2.zero, step);
            Assert.Greater(TurnBetween(new float2(0f, 1f), tank.Facing), 0f, "turned standing");
        }

        [Test]
        public void InstantLocomotion_StepsAtFullSpeed_ButNeverPastTheGoal()
        {
            var instant = Driving(Locomotion.Instant, default, 0.4f, 5f, new float2(1f, 0f), 0.1f);
            var far = new PathLeg(float2.zero, new float2(10f, 0f), float2.zero, 0f, 10f, true);
            MathAssert.Near(new float2(0.5f, 0f), instant.Drive(far));

            var near = new PathLeg(float2.zero, new float2(0.2f, 0f), float2.zero, 0f, 0.2f, true);
            MathAssert.Near(new float2(0.2f, 0f), instant.Drive(near));
        }

        [Test]
        public void WedgedVehicle_TurnsOutOnce_BeforeGivingUp()
        {
            var progress = new MoveProgress { Stalled = MoveProgress.GiveUpSeconds };
            var drive = new LocomotionState();

            Assert.IsTrue(progress.TryTurnOut(Tracks, ref drive, MoveProgress.WallHold.Wedged), "turns out");
            Assert.IsTrue(drive.IsManoeuvring, "driving the other way");
            Assert.AreEqual(0f, progress.Stalled, "with the clock restarted");
            Assert.IsFalse(progress.TryTurnOut(Tracks, ref drive, MoveProgress.WallHold.Wedged), "once until it gets closer");
            Assert.IsFalse(new MoveProgress().TryTurnOut(Tracks, ref drive, MoveProgress.WallHold.None), "not wedged");
            Assert.IsFalse(new MoveProgress().TryTurnOut(Locomotion.Instant, ref drive, MoveProgress.WallHold.Wedged),
                "no hull to turn");
        }

        [Test]
        public void TrackedHull_TurningOut_ReversesInsteadOfPivoting()
        {
            var tank = Driving(Tracks, TrackedChassis, TankRadius, 6f, new float2(0f, 1f));
            Tracks.TurnOut(ref tank.State);
            var behind = new PathLeg(float2.zero, new float2(0f, -40f), float2.zero, 0f, 40f, true);

            var step = float2.zero;
            for (var frame = 0; frame < 30; frame++)
            {
                step = tank.Drive(behind);
            }

            Assert.Less(tank.State.Speed, -1f, "backing up");
            Assert.Less(math.dot(step, new float2(0f, 1f)), 0f, "away from the wall ahead");
        }
    }
}
