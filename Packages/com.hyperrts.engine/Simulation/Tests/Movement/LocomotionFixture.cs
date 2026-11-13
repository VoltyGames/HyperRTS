using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static HyperRTS.Simulation.Tests.TestVehicles;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Shared setup and helpers for the LocomotionTests family.</summary>
    public abstract class LocomotionFixture : SimulationFixture
    {
        protected const float TruckRadius = 1.25f;
        protected const float TruckTurnRadius = 9f;
        protected const float TankRadius = 1.85f;

        protected static readonly Chassis TrackedChassis = new() { Pivots = true, ReverseRun = 10f };

        protected static readonly Chassis TruckChassis = new()
        {
            Wheelbase = 4.5f, RearAxle = 1.5f, TurnRadius = TruckTurnRadius, ReverseRun = 6f,
        };

        protected static Driver Driving(in Locomotion handling, in Chassis chassis, float radius, float topSpeed,
            float2 facing, float deltaTime = TestWorld.FrameTime) =>
            new(handling, new NavAgent { Radius = radius, Chassis = chassis }, topSpeed, deltaTime) { Facing = facing };

        protected Entity SpawnVehicle(float3 position, float radius, float hullLength, Locomotion locomotion,
            Chassis chassis)
        {
            var vehicle = World.SpawnUnit(1, position, new TestUnit { Speed = 8f, Radius = radius });
            World.Set(vehicle, new NavAgent
            {
                Radius = radius, HullLength = hullLength, Spacing = UnitAuthoring.DefaultSpacing, Chassis = chassis, IsVehicle = true,
                Mass = chassis.Pivots ? 62f : 17f,
            });
            var writer = World.Writer(vehicle);
            LocomotionAuthoring.Add(writer, locomotion);
            return vehicle;
        }

        protected Entity SpawnTruck(float3 position) => SpawnVehicle(position, TruckRadius, 8f, Wheels, TruckChassis);

        protected Entity SpawnTank(float3 position) => SpawnVehicle(position, TankRadius, 6.9f, Tracks, TrackedChassis);

        // Ticks once so the order is under way before callers wait on arrival.
        protected void Order(Entity unit, float3 goal)
        {
            World.MoveTo(unit, goal);
            World.Tick();
        }

        protected float2 Facing(Entity unit) =>
            math.normalize(math.mul(World.Get<LocalTransform>(unit).Rotation, math.forward()).xz);

        protected float Speed(Entity unit) => World.Get<LocomotionState>(unit).Speed;

        protected bool Arrived(Entity unit) => !World.IsEnabled<MoveDestination>(unit);

        protected float SlowestUntilArrived(Entity unit, float cap)
        {
            var slowest = 0f;
            for (var t = 0f; t < cap && !Arrived(unit); t += TestWorld.FrameTime)
            {
                World.Tick();
                slowest = math.min(slowest, Speed(unit));
            }

            Assert.IsTrue(Arrived(unit), "arrived");
            return slowest;
        }

        protected static float TurnBetween(float2 a, float2 b) => math.acos(math.clamp(math.dot(a, b), -1f, 1f));
    }
}
