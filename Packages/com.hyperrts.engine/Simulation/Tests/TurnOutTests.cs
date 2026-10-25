using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>A vehicle held by walls backs out before it gives up its move.</summary>
    public class TurnOutTests
    {
        private const float TankRadius = 1.85f;

        private static readonly Locomotion Tracks = new()
        {
            Acceleration = 2.5f, Braking = 6f, TurnRate = math.radians(50f), Pivots = true,
            ReverseSpeed = 3f, ReverseDistance = 10f,
        };

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

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
            var drive = new LocomotionState();
            Tracks.TurnOut(ref drive);
            var facing = new float2(0f, 1f);
            var behind = new PathLeg(float2.zero, new float2(0f, -40f), float2.zero, 0f, 40f, true);

            var step = float2.zero;
            for (var frame = 0; frame < 30; frame++)
            {
                step = Tracks.Drive(ref drive, float2.zero, ref facing, behind, 6f, TankRadius, TestWorld.FrameTime);
            }

            Assert.Less(drive.Speed, -1f, "backing up");
            Assert.Less(math.dot(step, new float2(0f, 1f)), 0f, "away from the wall ahead");
        }

        [Test]
        public void TankPushedOutOfABuilding_InASlot_BacksOutAndArrives()
        {
            var tank = _world.SpawnUnit(1, float3.zero, speed: 8f, radius: TankRadius);
            _world.EntityManager.SetComponentData(tank, new NavAgent
            {
                Radius = TankRadius, HullLength = 6.9f, Spacing = UnitAuthoring.DefaultSpacing,
            });
            var writer = new EntityManagerWriter(_world.EntityManager, tank);
            LocomotionAuthoring.Add(ref writer, Tracks);
            _world.Tick();
            // A slot too narrow to pivot in, closed by a building that lands on the tank's nose.
            _world.SpawnObstacle(new float3(-4f, 0f, 1.5f), new float2(2f, 7f));
            _world.SpawnObstacle(new float3(4f, 0f, 1.5f), new float2(2f, 7f));
            _world.SpawnObstacle(new float3(0f, 0f, 4f), new float2(6f, 2f));
            _world.Tick();
            var goal = new float3(-30f, 0f, -20f);
            _world.MoveTo(tank, goal);
            _world.Tick();

            var turnedOut = false;
            var ended = _world.RunUntil(() =>
            {
                turnedOut |= _world.Get<LocomotionState>(tank).IsManoeuvring;
                return !_world.IsEnabled<MoveDestination>(tank);
            }, 40f);

            Assert.IsTrue(turnedOut, "wedged, it backed out");
            Assert.IsTrue(ended, "the move ended");
            Assert.IsFalse(_world.Get<Arrival>(tank).Stalled, "by arriving, not by giving up");
            Assert.Less(math.distance(_world.GetPosition(tank).xz, goal.xz), 2f, "at the goal");
        }
    }
}
