using System.Collections.Generic;
using System.Diagnostics;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation
{
    public partial class NavBenchmarkTests
    {
        // A pair counts as drivable when a search this long finds its way, so the benchmarks ask only for ways that exist.
        private const int DrivableCap = 100000;

        private static readonly NavAgent SupplyTruck = WithHullMargin(new NavAgent
        {
            Radius = 1.25f, HullLength = 8f, Spacing = UnitAuthoring.DefaultSpacing,
            Chassis = new Chassis { Wheelbase = 4f, RearAxle = 1.2f, TurnRadius = 10f, ReverseRun = 6f },
        });

        // A lane of the base: its centre line from one end to the other.
        private struct Lane
        {
            public float2 From;
            public float2 To;
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct DriveJob : IJob
        {
            [ReadOnly] public NavGrid Grid;
            public NavAgent Agent;
            public int Cap;
            public AxlePose Start;
            public DriveGoal Goal;
            public NativeList<PathWaypoint> Points;
            public NativeReference<int2> Result;

            public void Execute()
            {
                Points.Clear();
                var search = new DriveSearch(Agent.Access(Grid), Agent, Cap, Allocator.Temp);
                var found = search.Run(Start, Goal, 0f, Points);
                Result.Value = new int2(found ? 1 : 0, search.Expansions);
                search.Dispose();
            }
        }

        // Twenty 10 m buildings in the west strip, four across and five deep, on lanes 4 to 8 m wide. The map's
        // buildings reaching into the strip go first, so no bay is walled into a pocket no hull can reach.
        private List<Lane> SpawnBase(ref Random random, List<NavBox> buildings)
        {
            ClearWestStrip();
            var lanes = new List<Lane>();
            var x = -294f;
            for (var column = 0; column < 4; column++)
            {
                var z = -45f;
                for (var row = 0; row < 5; row++)
                {
                    var center = new float3(x + 5f, 0f, z + 5f);
                    _world.SpawnObstacle(center, new float2(10f));
                    buildings.Add(new NavBox(center, quaternion.identity, new float2(10f)));
                    var gap = random.NextInt(4, 9);
                    var middle = z + 10f + gap * 0.5f;
                    lanes.Add(new Lane { From = new float2(x - 2f, middle), To = new float2(x + 12f, middle) });
                    z += 10f + gap;
                }

                var aisle = random.NextInt(4, 9);
                var aisleX = x + 10f + aisle * 0.5f;
                lanes.Add(new Lane { From = new float2(aisleX, -45f), To = new float2(aisleX, z) });
                x += 10f + aisle;
            }

            _world.Tick();
            return lanes;
        }

        // The wall margin every baked unit gets.
        private static NavAgent WithHullMargin(NavAgent agent)
        {
            agent.WallMargin = agent.HullWallMargin;
            return agent;
        }

        private DriveJob NewDriveJob(NativeList<PathWaypoint> points, NativeReference<int2> result) => new()
        {
            Grid = _world.Grid(), Agent = SupplyTruck, Cap = DriveSearch.MaxExpansions, Points = points, Result = result,
        };

        // Draws starts and goals until a long search drives between them, counting the pairs it passed over.
        private void DrawDrivable(ref DriveJob job, System.Func<(AxlePose, DriveGoal)> draw, ref int undrivable)
        {
            job.Cap = DrivableCap;
            do
            {
                (job.Start, job.Goal) = draw();
                job.Run();
                undrivable += 1 - job.Result.Value.x;
            }
            while (job.Result.Value.x == 0);

            job.Cap = DriveSearch.MaxExpansions;
        }

        private void ClearWestStrip()
        {
            var query = _world.EntityManager.CreateEntityQuery(typeof(NavObstacle), typeof(LocalTransform));
            var obstacles = query.ToEntityArray(Allocator.Temp);
            foreach (var obstacle in obstacles)
            {
                if (_world.Get<LocalTransform>(obstacle).Position.x < -200f)
                {
                    _world.EntityManager.DestroyEntity(obstacle);
                }
            }

            obstacles.Dispose();
            query.Dispose();
        }

        private static AxlePose LanePose(ref Random random, List<Lane> lanes, in Chassis chassis)
        {
            var lane = lanes[random.NextInt(lanes.Count)];
            var along = math.normalize(lane.To - lane.From) * (random.NextBool() ? 1f : -1f);
            var center = math.lerp(lane.From, lane.To, random.NextFloat(0.2f, 0.8f));
            return chassis.Pose(center, along);
        }

        private static readonly float2[] Sides = { new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f) };

        // Beside one wall of a building, a metre off it, facing along it.
        private static DriveGoal Bay(ref Random random, List<NavBox> buildings)
        {
            var box = buildings[random.NextInt(buildings.Count)];
            var outward = Sides[random.NextInt(Sides.Length)];
            var along = new float2(-outward.y, outward.x) * (random.NextBool() ? 1f : -1f);
            return DriveGoal.ToPose(box.Center + outward * (5f + SupplyTruck.Radius + 1f), along);
        }

        [Test]
        public void DockApproaches_IntoABase_OneHundred()
        {
            var random = new Random(41);
            var buildings = new List<NavBox>();
            var lanes = SpawnBase(ref random, buildings);
            _world.AddAgentPrefabs(SupplyTruck);
            _world.Tick();
            var points = new NativeList<PathWaypoint>(64, Allocator.Persistent);
            var result = new NativeReference<int2>(Allocator.Persistent);
            var job = NewDriveJob(points, result);
            double total = 0.0, slowest = 0.0;
            long expanded = 0;
            int most = 0, failures = 0, capped = 0, undrivable = 0;
            for (var i = -1; i < 100; i++)
            {
                DrawDrivable(ref job, () => (LanePose(ref random, lanes, SupplyTruck.Chassis), Bay(ref random, buildings)),
                    ref undrivable);
                var watch = Stopwatch.StartNew();
                job.Run();
                var time = watch.Elapsed.TotalMilliseconds;
                if (i >= 0)
                {
                    total += time;
                    slowest = math.max(slowest, time);
                    expanded += result.Value.y;
                    most = math.max(most, result.Value.y);
                    failures += 1 - result.Value.x;
                    capped += result.Value.y >= DriveSearch.MaxExpansions ? 1 : 0;
                }
            }

            points.Dispose();
            result.Dispose();
            var summary = $"100 dock approaches: mean {total / 100.0:F3} ms, max {slowest:F3} ms, " +
                          $"expansions mean {expanded / 100} max {most}, failures {failures} ({capped} at the cap), " +
                          $"{undrivable} undrivable pairs redrawn";
            TestContext.WriteLine(summary);
            Assert.AreEqual(0, failures, summary);
            Assert.Less(total / 100.0, 3.0, $"mean {total / 100.0:F3} ms, under twice 1.5 ms");
            Assert.Less(slowest, 10.0, $"slowest {slowest:F3} ms, under twice 5 ms");
        }

        [Test]
        public void TruckMoves_InABase_TwoHundred()
        {
            var random = new Random(43);
            var lanes = SpawnBase(ref random, new List<NavBox>());
            _world.AddAgentPrefabs(SupplyTruck);
            _world.Tick();
            var points = new NativeList<PathWaypoint>(64, Allocator.Persistent);
            var result = new NativeReference<int2>(Allocator.Persistent);
            var job = NewDriveJob(points, result);
            var undrivable = 0;
            var before = Stats();
            double served = 0.0;
            for (var i = 0; i < 200; i++)
            {
                DrawDrivable(ref job, () => (LanePose(ref random, lanes, SupplyTruck.Chassis),
                    DriveGoal.ToPoint(SupplyTruck.Chassis.Center(LanePose(ref random, lanes, SupplyTruck.Chassis)))),
                    ref undrivable);
                var start = job.Start;
                var center = SupplyTruck.Chassis.Center(start);
                var truck = _world.SpawnUnit(1, new float3(center.x, 0f, center.y), 8f, SupplyTruck.Radius);
                _world.EntityManager.SetComponentData(truck, SupplyTruck);
                var yaw = math.atan2(start.Facing.x, start.Facing.y);
                _world.EntityManager.SetComponentData(truck,
                    LocalTransform.FromPositionRotation(new float3(center.x, 0f, center.y), quaternion.RotateY(yaw)));
                _world.Tick();
                var goal = job.Goal.Center;
                _world.EntityManager.SetComponentData(truck, new MoveDestination { Value = new float3(goal.x, 0f, goal.y) });
                _world.EntityManager.SetComponentEnabled<MoveDestination>(truck, true);
                for (var frame = 0; frame < 10 && _world.Get<PathState>(truck).Status != PathStatus.Ready; frame++)
                {
                    served += TimeSystem<PathfindingSystem>();
                }

                _world.EntityManager.DestroyEntity(truck);
            }

            points.Dispose();
            result.Dispose();
            var after = Stats();
            var searches = after.DriveSearches - before.DriveSearches;
            var failures = after.DriveFailures - before.DriveFailures;
            var expansions = after.DriveExpansions - before.DriveExpansions;
            var summary = $"200 truck moves: {searches / 200.0:F2} drive searches per move, " +
                          $"{expansions / 200} expansions per move, failures {failures}, " +
                          $"pathfinder {served / 200.0:F3} ms per move, {undrivable} undrivable pairs redrawn";
            TestContext.WriteLine(summary);
            Assert.AreEqual(0, failures, summary);
        }
    }
}
