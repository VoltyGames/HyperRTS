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
    public abstract class DriveSearchFixture : SimulationFixture
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

        protected Entity Spawn(NavAgent agent, Locomotion locomotion, float3 position, float yaw)
        {
            var unit = World.SpawnUnit(1, position, new TestUnit { Speed = 8f, Radius = agent.Radius });
            agent.WallMargin = agent.HullWallMargin;
            World.Set(unit, agent);
            World.Set(unit,
                LocalTransform.FromPositionRotation(position, quaternion.RotateY(yaw)));
            var writer = World.Writer(unit);
            LocomotionAuthoring.Add(writer, locomotion);
            return unit;
        }

        protected DynamicBuffer<PathWaypoint> Waypoints(Entity unit) => World.Buffer<PathWaypoint>(unit);

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
            var agent = World.Get<NavAgent>(unit);
            var transform = World.Get<LocalTransform>(unit);
            var axis = agent.SegmentAxis(transform.Rotation);
            return new NavCapsule(agent.Access(World.Grid()), axis).Depth(transform.Position.xz, axis);
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
            World.MoveTo(unit, goal);
            World.Tick();
            var drive = new Drive();
            var gear = 0f;
            for (var t = 0f; t < cap && World.IsEnabled<MoveDestination>(unit); t += TestWorld.FrameTime)
            {
                World.Tick();
                drive.Deepest = math.max(drive.Deepest, HullDepth(unit));
                var speed = World.Get<LocomotionState>(unit).Speed;
                if (math.abs(speed) > 0.1f && math.sign(speed) != gear)
                {
                    drive.GearChanges += gear != 0f ? 1 : 0;
                    gear = math.sign(speed);
                }

                drive.Reversed |= speed < -0.1f;
            }

            drive.Arrived = !World.IsEnabled<MoveDestination>(unit) &&
                            World.Get<Arrival>(unit).Reason == ArrivalReason.Reached;
            return drive;
        }

        protected NavAccess RegisteredAccess(NavAgent agent)
        {
            agent.WallMargin = agent.HullWallMargin;
            return World.Access(agent);
        }
    }
}
