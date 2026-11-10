using System.Collections.Generic;
using HyperRTS.Simulation.Navigation.Agents;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>
    /// Movement quality as numbers: supply trucks gathering, driving a base's lanes and sharing them with tanks and
    /// squads. Each scenario logs a <see cref="TripRecord"/> and asserts only a final bar, which stays at the measured
    /// baseline until the phase that fixes it. Run with the category Traffic.
    /// </summary>
    [Category("Traffic")]
    public partial class TrafficScenarioTests
    {
        private TestWorld _world;
        private TripRecord _record;
        private readonly Dictionary<Entity, float> _arrivedAt = new();

        // Owners 1 and 2 are teammates, so teammate traffic needs no second match.
        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 1 }, 400f);
            _record = new TripRecord(_world);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity SpawnTruck(float3 position, float yaw, byte owner = 1) =>
            _world.SpawnVehicle(owner, position, yaw, 6f, TestVehicles.SupplyTruckHull(), TestVehicles.SupplyTruck);

        private Entity SpawnTank(float3 position, float yaw, byte owner = 1) =>
            _world.SpawnVehicle(owner, position, yaw, 7f, TestVehicles.TankHull(), TestVehicles.Tracks);

        private void Send(Entity unit, float3 goal)
        {
            _world.Set(unit, new MoveDestination { Value = goal });
            _world.SetEnabled<MoveDestination>(unit, true);
        }

        private bool HasArrived(Entity unit) => !_world.IsEnabled<MoveDestination>(unit);

        // Steps until every tracked unit has ended its move, stamping each one's arrival; false at the cap.
        private bool DriveAll(float cap)
        {
            for (var t = 0f; t < cap; t += TestWorld.FrameTime)
            {
                _record.Tick();
                var all = true;
                foreach (var tally in _record.Units)
                {
                    var arrived = HasArrived(tally.Unit);
                    all &= arrived;
                    if (arrived && !_arrivedAt.ContainsKey(tally.Unit))
                    {
                        _arrivedAt[tally.Unit] = _record.Seconds;
                    }
                }

                if (all)
                {
                    return true;
                }
            }

            return false;
        }

        // The final bar is that every unit arrives; until the phase that fixes a scenario it asks for its baseline.
        private void Finish(string title, float cap, int baselineArrivals)
        {
            LogArrivals(title, DriveAll(cap));
            Assert.GreaterOrEqual(_arrivedAt.Count, baselineArrivals, "units arrived");
            Assert.AreEqual(0, _record.GiveUps, "no unit gave up");
        }

        private Entity TrackedTruck(string name, float3 start, float yaw)
        {
            var truck = SpawnTruck(start, yaw);
            _record.Track(name, truck);
            return truck;
        }

        private void LogArrivals(string title, bool arrived)
        {
            _record.Log(title);
            var times = new List<string>();
            foreach (var tally in _record.Units)
            {
                var at = _arrivedAt.TryGetValue(tally.Unit, out var seconds) ? $"{seconds:F1} s" : "never";
                times.Add($"{tally.Name} {at}");
            }

            UnityEngine.Debug.Log($"[{title}] all arrived {arrived}; arrivals: {string.Join(", ", times)}");
        }

        private float3 Position(Entity unit) => _world.GetPosition(unit);
    }
}
