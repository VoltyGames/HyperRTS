using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Reach;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources.Gathering;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>
    /// Reachability per layer, goals snapping to open ground on the start's side, grid changes re-planning only the
    /// paths near them, and walking up to a footprint measured hull edge to footprint edge.
    /// </summary>
    public class NavReachTests : SimulationFixture
    {
        private const float TruckRadius = 1.25f;
        private const float TruckLength = 10f;
        private const float HalfSegment = TruckLength * 0.5f - TruckRadius;

        private static readonly NavAgent Infantry = new() { Radius = 0.5f };
        private static readonly NavAgent Ship = new() { Radius = 0.5f, Layer = NavLayer.Naval };
        private static readonly NavBox Yard = new(float3.zero, quaternion.identity, new float2(16f, 16f));

        [SetUp]
        public void Prepare()
        {
            World.AddAgentPrefabs(Infantry, Ship);
        }

        // Water across the whole map from z 5 to 5 + width.
        private void SpawnRiver(float width) =>
            World.SpawnNavArea(new float3(0f, 0f, 5f + width * 0.5f), new float2(200f, width), NavAreaKind.Water);

        [Test]
        public void River_SplitsTheGround_ShipsFollowIt_AndABridgeJoinsTheBanks()
        {
            SpawnRiver(10f);
            World.Tick();

            var grid = World.Grid();
            Assert.IsTrue(grid.IsReachable(Infantry, float3.zero, new float3(30f, 0f, -20f)), "same bank");
            Assert.IsFalse(grid.IsReachable(Infantry, float3.zero, new float3(0f, 0f, 20f)), "across the river");
            Assert.IsTrue(grid.IsReachable(Ship, new float3(-50f, 0f, 10f), new float3(50f, 0f, 10f)), "along it");

            World.SpawnNavArea(new float3(0f, 0f, 10f), new float2(6f, 12f), NavAreaKind.Deck);
            World.Tick();

            Assert.IsTrue(World.Grid().IsReachable(Infantry, float3.zero, new float3(0f, 0f, 20f)), "over the bridge");
        }

        [Test]
        public void GoalInTheRiver_SnapsToTheBankTheUnitIsOn()
        {
            SpawnRiver(10f);
            World.Tick();
            var unit = World.SpawnUnit(1, new float3(0f, 0f, -10f));

            // The far bank at z 15 is nearer this goal than the near one at z 5.
            World.MoveTo(unit, new float3(0f, 0f, 14f));
            World.Run(6f);

            var position = World.GetPosition(unit);
            Assert.Less(position.z, 5f, "stays on its own bank");
            Assert.Greater(position.z, 3f, "drove up to the water");
            Assert.IsFalse(World.IsEnabled<MoveDestination>(unit), "and stopped there");
        }

        [Test]
        public void GoalFarAcrossTheWater_LeavesTheUnitWhereItIs()
        {
            SpawnRiver(90f);
            World.Tick();
            var start = new float3(0f, 0f, -10f);
            var unit = World.SpawnUnit(1, start);

            World.MoveTo(unit, new float3(0f, 0f, 97f));
            World.Run(1f);

            var moved = math.distance(World.GetPosition(unit).xz, start.xz);
            Assert.Less(moved, 0.5f, "no ground near the goal to head for");
            Assert.IsFalse(World.IsEnabled<MoveDestination>(unit), "the move ends at once");
        }

        [Test]
        public void GoalInsideABuilding_StopsAtNearestOpenGround()
        {
            var building = new float3(10f, 0f, 0f);
            World.SpawnBuilding(2, building, new float2(6f, 6f));
            var unit = World.SpawnUnit(1, float3.zero);

            World.SetEnabled<Selected>(unit, true);
            World.Command(1, new PlayerCommand { Type = CommandType.Smart, Position = building });
            World.Run(6f);

            Assert.IsFalse(World.IsEnabled<ActiveOrder>(unit));
            Assert.IsTrue(World.Grid().IsWalkable(World.GetPosition(unit)));
            Assert.Less(math.distance(World.GetPosition(unit).xz, building.xz), 5f);
        }

        [Test]
        public void BuildingPlaced_RePlansOnlyThePathsPassingIt()
        {
            var far = Drive(new float3(-50f, 0f, -50f), new float3(50f, 0f, -50f));
            var crossed = Drive(new float3(-50f, 0f, 50f), new float3(50f, 0f, 50f));
            World.Run(0.2f);
            Mark(far);
            Mark(crossed);
            var version = World.Grid().Version;

            World.SpawnBuilding(2, new float3(0f, 0f, 50f), new float2(6f, 6f));
            World.Tick();

            Assert.Greater(World.Grid().Version, version);
            var kept = World.Get<PathState>(far);
            Assert.AreEqual(PathStatus.Ready, kept.Status);
            Assert.AreEqual(World.Grid().Version, kept.GridVersion, "revalidated on the new grid");
            Assert.AreEqual(1f, kept.Goal.y, "kept, not searched again");
            Assert.AreEqual(0f, World.Get<PathState>(crossed).Goal.y, "searched again round the building");
            Assert.Greater(World.Buffer<PathWaypoint>(crossed).Length, 1, "with a corner");
        }

        private Entity Drive(float3 start, float3 goal)
        {
            var unit = World.SpawnUnit(1, start);
            World.MoveTo(unit, goal);
            return unit;
        }

        // A search writes the goal it planned for, so a mark on its unused height shows whether one ran since.
        private void Mark(Entity unit)
        {
            var path = World.Get<PathState>(unit);
            Assert.AreEqual(PathStatus.Ready, path.Status);
            path.Goal.y = 1f;
            World.Set(unit, path);
        }

        [Test]
        public void EdgePoint_LiesJustOutsideTheNearestSide()
        {
            Assert.AreEqual(new float2(10f, 3f), Yard.EdgePoint(new float2(30f, 3f), 2f), "beside a side");
            Assert.AreEqual(new float2(1f, -10f), Yard.EdgePoint(new float2(1f, -6f), 2f), "from inside, the nearest side");
            var corner = Yard.EdgePoint(new float2(20f, 20f), 1f);
            Assert.AreEqual(1f, math.distance(corner, new float2(8f, 8f)), 1e-4f, "round a corner");
        }

        [Test]
        public void TurnedFootprint_MeasuresInItsOwnFrame()
        {
            var turned = new NavBox(float3.zero, quaternion.RotateY(math.radians(45f)), new float2(4f, 4f));
            var corner = 2f * math.SQRT2;

            Assert.AreEqual(1f, turned.Gap(new float2(corner + 1f, 0f), new float2(corner + 1f, 0f)), 1e-4f);
        }

        [Test]
        public void LongTruck_DeliversAtTheDropOffEdge_DrivingForward_AndNeverEntersIt()
        {
            var supplies = World.CreateSupplies();
            var dropOff = World.SpawnBuilding(1, new float3(-30f, 0f, 0f), new float2(16f, 16f));
            World.EntityManager.AddComponent<ResourceDropOff>(dropOff);
            var node = World.SpawnNode(new float3(10f, 0f, 0f), supplies, 100);
            var truck = World.MakeHarvester(SpawnTruck(new float3(-8f, 0f, 0f)), 10, 10f);
            var footprint = new NavBox(new float3(-30f, 0f, 0f), quaternion.identity, new float2(16f, 16f));
            World.Order(truck, OrderType.Gather, node);

            var closest = float.MaxValue;
            var reversed = false;
            for (var t = 0f; t < 60f && World.GetResources(1, supplies) < 20; t += TestWorld.FrameTime)
            {
                World.Tick();
                var transform = World.Get<LocalTransform>(truck);
                var axis = World.Get<NavAgent>(truck).SegmentAxis(transform.Rotation);
                closest = math.min(closest, footprint.Gap(transform.Position.xz - axis, transform.Position.xz + axis));
                // Backing off a corner it noses into is the wall reflex, not a reverse leg, so only the legs count.
                var drive = World.Get<LocomotionState>(truck);
                reversed |= drive.Reverse || drive.InThreePointTurn;
            }

            Assert.GreaterOrEqual(World.GetResources(1, supplies), 20, "two loads delivered");
            Assert.GreaterOrEqual(closest, TruckRadius - 0.05f, $"the hull never entered the footprint ({closest:F2} m)");
            Assert.IsFalse(reversed, "the round trips drive forward");
        }

        [Test]
        public void Builder_WalledOffFromItsSite_GivesUpTheOrder()
        {
            // A ring of walls round the site, 4 m clear of it, with no way in.
            var site = World.SpawnBuilding(1, new float3(20f, 0f, 0f), new float2(4f, 4f), complete: false,
                buildTime: 60f);
            World.SpawnObstacle(new float3(20f, 0f, 7f), new float2(16f, 2f));
            World.SpawnObstacle(new float3(20f, 0f, -7f), new float2(16f, 2f));
            World.SpawnObstacle(new float3(13f, 0f, 0f), new float2(2f, 12f));
            World.SpawnObstacle(new float3(27f, 0f, 0f), new float2(2f, 12f));
            var dozer = World.MakeBuilder(World.SpawnUnit(1, float3.zero));
            World.Order(dozer, OrderType.Build, site);

            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<ActiveOrder>(dozer), 20f), "the order finished");
            Assert.IsFalse(World.IsEnabled<MoveDestination>(dozer), "and the walk with it");
            Assert.AreEqual(0f, World.Get<ConstructionProgress>(site).Value, "nothing was built");
        }

        private Entity SpawnTruck(float3 position)
        {
            var truck = World.SpawnUnit(1, position, speed: 8f, radius: TruckRadius);
            World.Set(truck, LocalTransform.FromPositionRotation(position,
                quaternion.RotateY(math.PI * 0.5f)));
            World.Set(truck, new NavAgent
            {
                Radius = TruckRadius, HullLength = TruckLength, Spacing = UnitAuthoring.DefaultSpacing, Mass = 17f,
                IsVehicle = true,
                Chassis = new Chassis { Wheelbase = 4f, RearAxle = 1.2f, TurnRadius = 10f, ReverseRun = 6f },
            });
            LocomotionAuthoring.Add(World.Writer(truck), TestVehicles.ShortTruck);
            return truck;
        }
    }
}
