using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The tracked and wheeled handling the drive and locomotion tests share.</summary>
    public static class TestVehicles
    {
        public static readonly Locomotion Tracks = new()
        {
            Acceleration = 2.5f, Braking = 6f, TurnRate = math.radians(50f), ReverseSpeed = 3f,
        };

        public static readonly Locomotion Wheels = new()
        {
            Acceleration = 3f, Braking = 6f, TurnRate = math.radians(40f), SteerRate = math.radians(45f),
            ReverseSpeed = 3f,
        };

        /// <summary>The game's supply truck handling.</summary>
        public static readonly Locomotion SupplyTruck = new()
        {
            Acceleration = 2.2f, Braking = 6f, TurnRate = math.radians(35f), SteerRate = math.radians(45f),
            ReverseSpeed = 3f,
        };

        /// <summary>The game's supply truck hull: 8 m long, a 6 m turn, reversing 6 m.</summary>
        public static NavAgent SupplyTruckHull() => Hull(new NavAgent
        {
            Radius = 1.25f, HullLength = 8f, Mass = 17f,
            Chassis = new Chassis { Wheelbase = 4.6f, RearAxle = 2f, TurnRadius = 6f, ReverseRun = 6f },
        });

        /// <summary>Wheeled handling for the dock and reach tests' short-wheelbase truck.</summary>
        public static readonly Locomotion ShortTruck = new()
        {
            Acceleration = 3f, Braking = 6f, TurnRate = math.radians(35f), SteerRate = math.radians(45f),
            ReverseSpeed = 3f,
        };

        /// <summary>A short-wheelbase truck hull of the given size, with a 6 m turn and a 6 m reverse.</summary>
        public static NavAgent ShortTruckHull(float radius, float length) => Hull(new NavAgent
        {
            Radius = radius, HullLength = length, Mass = 17f,
            Chassis = new Chassis { Wheelbase = 4f, RearAxle = 1.2f, TurnRadius = 6f, ReverseRun = 6f },
        });

        /// <summary>A main battle tank hull that pivots on the spot.</summary>
        public static NavAgent TankHull() => Hull(new NavAgent
        {
            Radius = 1.85f, HullLength = 7.9f, Mass = 62f,
            Chassis = new Chassis { Pivots = true, ReverseRun = 10f },
        });

        /// <summary>A transform at <paramref name="position"/> facing <paramref name="yaw"/> radians.</summary>
        public static LocalTransform At(float3 position, float yaw) =>
            LocalTransform.FromPositionRotation(position, quaternion.RotateY(yaw));

        /// <summary>A vehicle standing at <paramref name="at"/>.</summary>
        public static Entity SpawnVehicle(this TestWorld world, byte owner, LocalTransform at, float speed,
            NavAgent agent, Locomotion locomotion)
        {
            var vehicle = world.SpawnUnit(owner, at.Position, new TestUnit { Speed = speed, Radius = agent.Radius });
            world.Set(vehicle, agent);
            world.Set(vehicle, at);
            LocomotionAuthoring.Add(world.Writer(vehicle), locomotion);
            return vehicle;
        }

        public static NavAgent Hull(NavAgent agent)
        {
            agent.IsVehicle = true;
            agent.Spacing = UnitAuthoring.DefaultSpacing;
            agent.WallMargin = agent.HullWallMargin;
            return agent;
        }
    }
}
