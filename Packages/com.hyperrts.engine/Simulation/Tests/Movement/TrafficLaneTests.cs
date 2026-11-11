using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Paths;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Trucks in a base's narrow lanes: routes against the shortest, and a lane held by a busy vehicle.</summary>
    [Category("Traffic")]
    public class TrafficLaneTests : TrafficScenarioFixture
    {
        private const int BaseMoves = 50;
        private const float MoveTimeout = 90f;
        private const float BlockSize = 14f;
        private static readonly float[] ColumnGaps = { 4f, 6f, 8f, 5f };
        private static readonly float[] RowGaps = { 4f, 6f, 8f };

        // One truck's run of random moves, with the planned length of the move it is on.
        private sealed class RouteTruck
        {
            public Entity Unit;
            public TripRecord.Tally Tally;
            public float3 Goal;
            public float Shortest;
            public float StartedAt;
            public int Moves;
            public bool Active;
            public bool Planned;
        }

        [Test]
        public void Trucks_DriveTwoHundredMovesThroughABase_AgainstTheShortestOpenRoute()
        {
            var half = BuildBase();
            var hull = TestVehicles.SupplyTruckHull();
            var trucks = new List<RouteTruck>();
            for (var i = 0; i < 4; i++)
            {
                var corner = new float3((i % 2 * 2 - 1) * (half.x + 15f), 0f, (i / 2 * 2 - 1) * (half.y + 15f));
                var unit = SpawnTruck(corner, 0f);
                trucks.Add(new RouteTruck { Unit = unit, Tally = _record.Track($"truck {i}", unit) });
            }

            _world.Tick();
            using var open = new OpenRoutes(_world, hull);
            var region = open.Region(open.Cell(Position(trucks[0].Unit)));
            var random = new Random(29);
            var ratios = new List<float>();
            var timeouts = 0;
            for (var t = 0f; t < 2400f && trucks.Exists(truck => truck.Moves < BaseMoves || truck.Active); t += TestWorld.FrameTime)
            {
                _record.Tick();
                foreach (var truck in trucks)
                {
                    timeouts += Advance(truck, open, region, half, ref random, ratios);
                }
            }

            _record.Log("base lanes");
            UnityEngine.Debug.Log($"[base lanes] {ratios.Count} routes planned of {trucks.Count * BaseMoves}, " +
                                           $"mean planned over shortest {ratios.DefaultIfEmpty(0f).Average():F2}, worst {ratios.DefaultIfEmpty(0f).Max():F2}, " +
                                           $"timeouts {timeouts}");
            Assert.Greater(ratios.Count, 0, "routes were planned");
            Assert.Greater(_record.Trips, 0, "trucks arrived");
        }

        // Plans, arrivals and timeouts of one truck this step; returns 1 when its move timed out.
        private int Advance(RouteTruck truck, OpenRoutes open, int region, float2 half, ref Random random,
            List<float> ratios)
        {
            if (truck.Active && !truck.Planned)
            {
                ReadPlan(truck, ratios);
            }

            var timedOut = truck.Active && _record.Seconds - truck.StartedAt > MoveTimeout;
            var arrived = truck.Active && HasArrived(truck.Unit);
            if (arrived)
            {
                _record.CountTrip(truck.Tally);
            }

            truck.Active &= !arrived && !timedOut;
            if (!truck.Active && truck.Moves < BaseMoves)
            {
                StartMove(truck, open, region, half, ref random);
            }

            return timedOut ? 1 : 0;
        }

        private void StartMove(RouteTruck truck, OpenRoutes open, int region, float2 half, ref Random random)
        {
            truck.Goal = RandomPoint(open, region, half, ref random);
            truck.Shortest = open.Length(Position(truck.Unit), truck.Goal);
            truck.StartedAt = _record.Seconds;
            truck.Planned = false;
            truck.Active = true;
            truck.Moves++;
            _world.Send(truck.Unit, truck.Goal);
        }

        private void ReadPlan(RouteTruck truck, List<float> ratios)
        {
            var state = _world.Get<PathState>(truck.Unit);
            var waypoints = _world.Buffer<PathWaypoint>(truck.Unit);
            if (state.Status != PathStatus.Ready || !math.all(state.Goal == truck.Goal) || waypoints.Length == 0)
            {
                return;
            }

            truck.Planned = true;
            if (truck.Shortest > 0f)
            {
                var planned = math.distance(Position(truck.Unit).xz, waypoints[0].Position.xz) +
                              PathState.Measure(waypoints);
                ratios.Add(planned / truck.Shortest);
            }
        }

        // Open ground the truck can stand on, in the base's region and clear of walls.
        private static float3 RandomPoint(OpenRoutes open, int region, float2 half, ref Random random)
        {
            while (true)
            {
                var offset = random.NextFloat2(-half - 20f, half + 20f);
                var cell = open.Cell(new float3(offset.x, 0f, offset.y));
                if (open.IsOpen(cell) && open.Region(cell) == region && open.ClearOf(cell) >= 3f)
                {
                    return open.Point(cell);
                }
            }
        }

        // Five columns by four rows of blocks on lanes 4 to 8 m wide; returns the half extents.
        private float2 BuildBase()
        {
            var width = 5f * BlockSize + ColumnGaps.Sum();
            var depth = 4f * BlockSize + RowGaps.Sum();
            var x = -width * 0.5f;
            for (var column = 0; column < 5; column++)
            {
                var z = -depth * 0.5f;
                for (var row = 0; row < 4; row++)
                {
                    _world.SpawnObstacle(new float3(x + BlockSize * 0.5f, 0f, z + BlockSize * 0.5f),
                        new float2(BlockSize));
                    z += BlockSize + (row < 3 ? RowGaps[row] : 0f);
                }

                x += BlockSize + (column < 4 ? ColumnGaps[column % ColumnGaps.Length] : 0f);
            }

            return new float2(width, depth) * 0.5f;
        }

        // A wall across x = 0 with a 6 m lane at z 0 and its ends 33.5 m out, so going round is 1.5 times the lane.
        [Test]
        public void Truck_MeetsABusyVehicleInASixMetreLane_WithAWayRoundOneAndAHalfTimesLonger()
        {
            _world.SpawnObstacle(new float3(0f, 0f, 18.25f), new float2(4f, 30.5f));
            _world.SpawnObstacle(new float3(0f, 0f, -18.25f), new float2(4f, 30.5f));
            var dozer = SpawnTank(float3.zero, 0f);
            _world.AddWeapon(dozer, Stance.HoldPosition);
            var truck = SpawnTruck(new float3(-30f, 0f, 0f), math.PI * 0.5f);
            _record.Track("truck", truck);
            _world.Tick();
            _world.Send(truck, new float3(30f, 0f, 0f));

            Finish("blocked lane", 120f, 0);
        }
    }
}
