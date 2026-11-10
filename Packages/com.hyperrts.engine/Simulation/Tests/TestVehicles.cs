using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
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

        /// <summary>The game's supply truck hull: 8 m long, a 10 m turn, reversing 6 m.</summary>
        public static NavAgent SupplyTruckHull() => Hull(new NavAgent
        {
            Radius = 1.25f, HullLength = 8f,
            Chassis = new Chassis { Wheelbase = 4.6f, RearAxle = 2f, TurnRadius = 10f, ReverseRun = 6f },
        });

        /// <summary>A main battle tank hull that pivots on the spot.</summary>
        public static NavAgent TankHull() => Hull(new NavAgent
        {
            Radius = 1.85f, HullLength = 7.9f, Chassis = new Chassis { Pivots = true, ReverseRun = 10f },
        });

        /// <summary>A vehicle standing at <paramref name="position"/>, facing <paramref name="yaw"/> radians.</summary>
        public static Entity SpawnVehicle(this TestWorld world, byte owner, float3 position, float yaw, float speed,
            NavAgent agent, Locomotion locomotion)
        {
            var vehicle = world.SpawnUnit(owner, position, speed, agent.Radius);
            world.Set(vehicle, agent);
            world.Set(vehicle, LocalTransform.FromPositionRotation(position, quaternion.RotateY(yaw)));
            LocomotionAuthoring.Add(world.Writer(vehicle), locomotion);
            return vehicle;
        }

        private static NavAgent Hull(NavAgent agent)
        {
            agent.Spacing = UnitAuthoring.DefaultSpacing;
            agent.WallMargin = agent.HullWallMargin;
            return agent;
        }
    }
}
