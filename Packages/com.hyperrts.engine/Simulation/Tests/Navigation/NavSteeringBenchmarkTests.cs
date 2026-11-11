using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>Benchmarks of steering.</summary>
    [Category("Benchmark")]
    public class NavSteeringBenchmarkTests : NavBenchmarkFixture
    {
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
            _world.Send(unit, new float3(-at.x, 0f, at.z), group);
        }
    }
}
