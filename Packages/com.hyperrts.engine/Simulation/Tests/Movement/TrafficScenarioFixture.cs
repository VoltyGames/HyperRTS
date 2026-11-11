using System.Collections.Generic;
using HyperRTS.Simulation.Navigation.Agents;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Shared setup and helpers for the TrafficScenarioTests family.</summary>
    [Category("Traffic")]
    public abstract class TrafficScenarioFixture : SimulationFixture
    {
        protected TripRecord _record;
        protected readonly Dictionary<Entity, float> _arrivedAt = new();

        // Owners 1 and 2 are teammates, so teammate traffic needs no second match. Squad members keep off walls and
        // allied hulls every frame.
        protected override byte[] Teams => new byte[] { 1, 1 };

        protected override float MapSize => 400f;

        [SetUp]
        public void StartRecord()
        {
            _record = new TripRecord(World);
            var footing = new SquadFootingCheck(World);
            World.CheckEveryTick(footing.AssertOnOpenGround);
            World.CheckEveryTick(footing.AssertOffHulls);
        }

        protected Entity SpawnTruck(float3 position, float yaw, byte owner = 1) =>
            World.SpawnVehicle(owner, TestVehicles.At(position, yaw), 6f, TestVehicles.SupplyTruckHull(),
                TestVehicles.SupplyTruck);

        protected Entity SpawnTank(float3 position, float yaw, byte owner = 1) =>
            World.SpawnVehicle(owner, TestVehicles.At(position, yaw), 7f, TestVehicles.TankHull(),
                TestVehicles.Tracks);

        protected bool HasArrived(Entity unit) => !World.IsEnabled<MoveDestination>(unit);

        // Steps until every tracked unit has ended its move, stamping each one's arrival; false at the cap.
        protected bool DriveAll(float cap)
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
        protected void Finish(string title, float cap, int baselineArrivals)
        {
            LogArrivals(title, DriveAll(cap));
            Assert.GreaterOrEqual(_arrivedAt.Count, baselineArrivals, "units arrived");
            Assert.AreEqual(0, _record.GiveUps, "no unit gave up");
        }

        // Wheels can't move sideways, so nothing may slide a truck; foot units no longer do.
        protected void AssertNoWheelsSlid()
        {
            foreach (var tally in _record.Units)
            {
                if (World.Get<NavAgent>(tally.Unit).Chassis.IsWheeled)
                {
                    Assert.Less(tally.Sideways, 0.2f, $"{tally.Name} slid sideways");
                }
            }
        }

        protected Entity TrackedTruck(string name, float3 start, float yaw)
        {
            var truck = SpawnTruck(start, yaw);
            _record.Track(name, truck);
            return truck;
        }

        protected void LogArrivals(string title, bool arrived)
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

        protected float3 Position(Entity unit) => World.GetPosition(unit);

        protected const float East = math.PI * 0.5f;

        protected const float West = -math.PI * 0.5f;
    }
}
