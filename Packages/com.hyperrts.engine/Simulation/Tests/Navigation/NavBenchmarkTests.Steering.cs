using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation
{
    public partial class NavBenchmarkTests
    {
        private static readonly Locomotion TankHandling = new()
        {
            Acceleration = 2.5f, Braking = 6f, TurnRate = math.radians(50f), ReverseSpeed = 3f,
        };

        [Test, Performance]
        public void SteeringFrame_FiveHundredUnits()
        {
            var units = new Entity[500];
            for (var i = 0; i < 500; i++)
            {
                units[i] = SpawnCrowdMember(i);
            }

            _world.Tick();
            for (var i = 0; i < 500; i++)
            {
                var start = _world.GetPosition(units[i]);
                _world.EntityManager.SetComponentData(units[i],
                    new MoveDestination { Value = new float3(start.x, 0f, -start.z), Group = i < 250 ? 1 : 2 });
                _world.EntityManager.SetComponentEnabled<MoveDestination>(units[i], true);
            }

            // Until the heads of both crowds meet, so most units steer round someone.
            _world.Run(5f);
            var median = BenchmarkKit.Median("SteeringFrame", _world.UpdateSystem<MovementSystem>, () => _world.Tick(),
                null, "MovementSystem.Move", "Steering.Pick");

            TestContext.WriteLine($"500-unit steering frame: median {median:F3} ms");
            Assert.Less(median, 3.0, $"median {median:F3} ms, under twice 1.5 ms");
        }

        // Each crowd is 200 infantry beside 50 tanks in the clear west strip, the halves facing each other.
        private Entity SpawnCrowdMember(int i)
        {
            var north = i >= 250;
            var index = i % 250;
            var sign = north ? 1f : -1f;
            if (index < 200)
            {
                var soldier = new float3(-290f + index % 10 * 2f, 0f, sign * (45f + index / 10 * 2f));
                return _world.SpawnUnit(1, soldier, 5f, Infantry.Radius);
            }

            var slot = index - 200;
            var position = new float3(-268f + slot % 5 * 5f, 0f, sign * (45f + slot / 5 * 9f));
            var tank = _world.SpawnUnit(1, position, 7f, Tank.Radius);
            var agent = Tank;
            agent.HullLength = 7.5f;
            agent.Mass = 62f;
            agent.Spacing = UnitAuthoring.DefaultSpacing;
            agent.Chassis = new Chassis { Pivots = true, ReverseRun = 10f };
            _world.EntityManager.SetComponentData(tank, agent);
            _world.EntityManager.SetComponentData(tank,
                LocalTransform.FromPositionRotation(position, quaternion.RotateY(north ? math.PI : 0f)));
            var writer = new EntityWriter(_world.EntityManager, tank);
            LocomotionAuthoring.Add(writer, TankHandling);
            return tank;
        }

        // 1,300 tanks and trucks and 2,700 soldiers, half in each end strip, marching to the other.
        [Test, Performance]
        public void MovementFrame_FourThousandUnits_AThirdOfThemVehicles()
        {
            _world.AddAgentPrefabs(TestVehicles.TankHull(), TestVehicles.SupplyTruckHull());
            var marchers = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                marchers += SpawnVehicleStrip(side, 650) + SpawnFootStrip(side, 1350);
            }

            _world.Tick();
            Assert.AreEqual(4000, marchers, "every unit exists");
            _world.Run(10f);
            var median = BenchmarkKit.Median("MovementFrame4000", _world.UpdateSystem<MovementSystem>, () => _world.Tick(),
                null, "MovementSystem.Move", "Steering.Pick");

            UnityEngine.Debug.Log($"[scale] 4000-unit movement frame: median {median:F3} ms " +
                                           "(MovementSystem.Move and Steering.Pick are in the performance results)");
        }

        // Seven columns of vehicles facing the far strip, tanks and trucks alternating.
        private int SpawnVehicleStrip(float side, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var position = new float3(side * (-295f + i % 7 * 9f), 0f, -295f + i / 7 * 5f);
                var tank = i % 2 == 0;
                var vehicle = _world.SpawnVehicle(1, position, side * math.PI * -0.5f, 7f,
                    tank ? TestVehicles.TankHull() : TestVehicles.SupplyTruckHull(),
                    tank ? TestVehicles.Tracks : TestVehicles.SupplyTruck);
                March(vehicle, side, side < 0f ? 1 : 3);
            }

            return count;
        }

        // Soldiers 1.6 m apart in the rows beyond the vehicles.
        private int SpawnFootStrip(float side, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var position = new float3(side * (-295f + i % 40 * 1.6f), 0f, 180f + i / 40 * 1.6f);
                March(_world.SpawnUnit(1, position, 5f, Infantry.Radius), side, side < 0f ? 2 : 4);
            }

            return count;
        }

        private void March(Entity unit, float side, int group)
        {
            var at = _world.GetPosition(unit);
            _world.Set(unit, new MoveDestination { Value = new float3(-at.x, 0f, at.z), Group = group });
            _world.SetEnabled<MoveDestination>(unit, true);
        }
    }
}
