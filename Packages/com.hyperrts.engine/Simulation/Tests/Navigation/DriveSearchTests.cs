using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>
    /// The drive planner: easy paths run no search, while yards, tight corners, slots and wedges get planned
    /// manoeuvres with reverse legs that never touch a wall. The search's shortcut and cap.
    /// </summary>
    public partial class DriveSearchTests
    {
        private const float WallSlack = 0.05f;

        private static readonly NavAgent Truck = new()
        {
            Radius = 1.25f, HullLength = 8f, Spacing = UnitAuthoring.DefaultSpacing,
            Chassis = new Chassis { Wheelbase = 4.5f, RearAxle = 1.5f, TurnRadius = 9f, ReverseRun = 6f },
        };

        private static readonly Locomotion Wheels = new()
        {
            Acceleration = 3f, Braking = 6f, TurnRate = math.radians(40f), SteerRate = math.radians(45f),
            ReverseSpeed = 3f,
        };

        private static readonly NavAgent Tank = new()
        {
            Radius = 1.85f, HullLength = 6.9f, Spacing = UnitAuthoring.DefaultSpacing,
            Chassis = new Chassis { Pivots = true, ReverseRun = 10f },
        };

        private static readonly Locomotion Tracks = new()
        {
            Acceleration = 2.5f, Braking = 6f, TurnRate = math.radians(50f), ReverseSpeed = 3f,
        };

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, 200f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity Spawn(NavAgent agent, Locomotion locomotion, float3 position, float yaw)
        {
            var unit = _world.SpawnUnit(1, position, 8f, agent.Radius);
            agent.WallMargin = agent.HullWallMargin;
            _world.EntityManager.SetComponentData(unit, agent);
            _world.EntityManager.SetComponentData(unit,
                LocalTransform.FromPositionRotation(position, quaternion.RotateY(yaw)));
            var writer = new EntityManagerWriter(_world.EntityManager, unit);
            LocomotionAuthoring.Add(ref writer, locomotion);
            return unit;
        }

        private PathStats Stats()
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            var handle = _world.World.GetExistingSystem<PathfindingSystem>();
            return _world.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle).Stats;
        }

        private DynamicBuffer<PathWaypoint> Waypoints(Entity unit) => _world.EntityManager.GetBuffer<PathWaypoint>(unit);

        private bool HasTightWaypoints(Entity unit)
        {
            foreach (var waypoint in Waypoints(unit))
            {
                if (waypoint.Tight)
                {
                    return true;
                }
            }

            return false;
        }

        private float HullDepth(Entity unit)
        {
            var agent = _world.Get<NavAgent>(unit);
            var transform = _world.Get<LocalTransform>(unit);
            var axis = agent.SegmentAxis(transform.Rotation);
            return new NavCapsule(agent.Access(_world.Grid()), axis).Depth(transform.Position.xz, axis);
        }

        private struct Drive
        {
            public bool Arrived;
            public float Deepest;
            public int GearChanges;
            public bool Reversed;
        }

        // Orders the move and drives until it ends, recording the deepest wall overlap and the gear changes.
        private Drive DriveTo(Entity unit, float3 goal, float cap)
        {
            _world.MoveTo(unit, goal);
            _world.Tick();
            var drive = new Drive();
            var gear = 0f;
            for (var t = 0f; t < cap && _world.IsEnabled<MoveDestination>(unit); t += TestWorld.FrameTime)
            {
                _world.Tick();
                drive.Deepest = math.max(drive.Deepest, HullDepth(unit));
                var speed = _world.Get<LocomotionState>(unit).Speed;
                if (math.abs(speed) > 0.1f && math.sign(speed) != gear)
                {
                    drive.GearChanges += gear != 0f ? 1 : 0;
                    gear = math.sign(speed);
                }

                drive.Reversed |= speed < -0.1f;
            }

            drive.Arrived = !_world.IsEnabled<MoveDestination>(unit) &&
                            _world.Get<Arrival>(unit).Reason == ArrivalReason.Reached;
            return drive;
        }

        private NavAccess RegisteredAccess(NavAgent agent)
        {
            agent.WallMargin = agent.HullWallMargin;
            _world.AddAgentPrefabs(agent);
            _world.Tick();
            return agent.Access(_world.Grid());
        }

        [Test]
        public void Shortcut_EndsAnOpenGroundSearch_WithinFiftyExpansions()
        {
            var access = RegisteredAccess(Truck);
            var search = new DriveSearch(access, Truck, DriveSearch.MaxExpansions, Allocator.Temp);
            var points = new NativeList<PathWaypoint>(64, Allocator.Temp);
            var start = Truck.Chassis.Pose(float2.zero, new float2(0f, 1f));

            var found = search.Run(start, DriveGoal.ToPose(new float2(20f, 30f), new float2(1f, 0f)), 0f, points);

            Assert.IsTrue(found, "found");
            Assert.LessOrEqual(search.Expansions, 50, "the curve ended it");
            Assert.Less(math.distance(points[points.Length - 1].Position.xz, new float2(20f, 30f)), 0.01f, "at the goal");
            points.Dispose();
            search.Dispose();
        }

        [Test]
        public void UnreachablePose_StopsAtTheCap()
        {
            // A courtyard walled all round, too small to hold the truck.
            _world.SpawnObstacle(new float3(30f, 0f, 36f), new float2(14f, 2f));
            _world.SpawnObstacle(new float3(30f, 0f, 24f), new float2(14f, 2f));
            _world.SpawnObstacle(new float3(24f, 0f, 30f), new float2(2f, 14f));
            _world.SpawnObstacle(new float3(36f, 0f, 30f), new float2(2f, 14f));
            var access = RegisteredAccess(Truck);
            var search = new DriveSearch(access, Truck, DriveSearch.MaxExpansions, Allocator.Temp);
            var points = new NativeList<PathWaypoint>(64, Allocator.Temp);
            var start = Truck.Chassis.Pose(float2.zero, new float2(0f, 1f));

            var found = search.Run(start, DriveGoal.ToPose(new float2(30f, 30f), new float2(1f, 0f)), 0f, points);

            Assert.IsFalse(found, "no way in");
            Assert.AreEqual(DriveSearch.MaxExpansions, search.Expansions, "stopped at the cap");
            Assert.AreEqual(0, points.Length, "added nothing");
            points.Dispose();
            search.Dispose();
        }

        [Test]
        public void PoseInsideAWall_CostsNoExpansions()
        {
            _world.SpawnObstacle(new float3(30f, 0f, 30f), new float2(12f, 12f));
            var access = RegisteredAccess(Truck);
            var search = new DriveSearch(access, Truck, DriveSearch.MaxExpansions, Allocator.Temp);
            var points = new NativeList<PathWaypoint>(64, Allocator.Temp);
            var start = Truck.Chassis.Pose(float2.zero, new float2(0f, 1f));

            var found = search.Run(start, DriveGoal.ToPose(new float2(30f, 30f), new float2(1f, 0f)), 0f, points);

            Assert.IsFalse(found, "no way into the building");
            Assert.AreEqual(0, search.Expansions, "gave up before searching");
            points.Dispose();
            search.Dispose();
        }

        [Test]
        public void OpenGround_PathHasNoTightWaypoints_AndRunsNoSearch()
        {
            var truck = Spawn(Truck, Wheels, float3.zero, 0f);
            _world.Tick();
            _world.MoveTo(truck, new float3(40f, 0f, 70f));
            _world.Tick(frames: 2);

            Assert.AreEqual(PathStatus.Ready, _world.Get<PathState>(truck).Status);
            Assert.IsFalse(HasTightWaypoints(truck), "drawn as planned");
            Assert.AreEqual(0, Stats().DriveSearches, "no search");
        }
    }
}
