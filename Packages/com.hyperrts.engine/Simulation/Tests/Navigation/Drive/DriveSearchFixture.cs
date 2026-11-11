using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static HyperRTS.Simulation.Tests.TestVehicles;

namespace HyperRTS.Simulation.Tests.Navigation.Drive
{
    /// <summary>Shared setup and helpers for the DriveSearchTests family.</summary>
    public abstract class DriveSearchFixture
    {
        protected const float WallSlack = 0.05f;

        protected static readonly NavAgent Truck = new()
        {
            Radius = 1.25f, HullLength = 8f, Spacing = UnitAuthoring.DefaultSpacing,
            Chassis = new Chassis { Wheelbase = 4.5f, RearAxle = 1.5f, TurnRadius = 9f, ReverseRun = 6f },
        };

        protected static readonly NavAgent Tank = new()
        {
            Radius = 1.85f, HullLength = 6.9f, Spacing = UnitAuthoring.DefaultSpacing,
            Chassis = new Chassis { Pivots = true, ReverseRun = 10f },
        };

        protected TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, 200f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        protected Entity Spawn(NavAgent agent, Locomotion locomotion, float3 position, float yaw)
        {
            var unit = _world.SpawnUnit(1, position, 8f, agent.Radius);
            agent.WallMargin = agent.HullWallMargin;
            _world.Set(unit, agent);
            _world.Set(unit,
                LocalTransform.FromPositionRotation(position, quaternion.RotateY(yaw)));
            var writer = _world.Writer(unit);
            LocomotionAuthoring.Add(writer, locomotion);
            return unit;
        }

        protected PathStats Stats()
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            var handle = _world.World.GetExistingSystem<PathfindingSystem>();
            return _world.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle).Stats;
        }

        protected DynamicBuffer<PathWaypoint> Waypoints(Entity unit) => _world.Buffer<PathWaypoint>(unit);

        protected bool HasTightWaypoints(Entity unit)
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

        protected float HullDepth(Entity unit)
        {
            var agent = _world.Get<NavAgent>(unit);
            var transform = _world.Get<LocalTransform>(unit);
            var axis = agent.SegmentAxis(transform.Rotation);
            return new NavCapsule(agent.Access(_world.Grid()), axis).Depth(transform.Position.xz, axis);
        }

        protected struct Drive
        {
            public bool Arrived;
            public float Deepest;
            public int GearChanges;
            public bool Reversed;
        }

        // Orders the move and drives until it ends, recording the deepest wall overlap and the gear changes.
        protected Drive DriveTo(Entity unit, float3 goal, float cap)
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

        protected NavAccess RegisteredAccess(NavAgent agent)
        {
            agent.WallMargin = agent.HullWallMargin;
            return _world.Access(agent);
        }
    }
}
