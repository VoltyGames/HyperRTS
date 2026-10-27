using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Units;
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
    public class NavReachTests
    {
        private const float TruckRadius = 1.25f;
        private const float TruckLength = 10f;
        private const float HalfSegment = TruckLength * 0.5f - TruckRadius;

        private static readonly NavAgent Infantry = new() { Radius = 0.5f };
        private static readonly NavAgent Ship = new() { Radius = 0.5f, Layer = NavLayer.Naval };
        private static readonly NavBox Yard = new(float3.zero, quaternion.identity, new float2(16f, 16f));

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.AddAgentPrefabs(Infantry, Ship);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        // Water across the whole map from z 5 to 5 + width.
        private void SpawnRiver(float width) =>
            _world.SpawnNavArea(new float3(0f, 0f, 5f + width * 0.5f), new float2(200f, width), NavAreaKind.Water);

        [Test]
        public void River_SplitsTheGround_ShipsFollowIt_AndABridgeJoinsTheBanks()
        {
            SpawnRiver(10f);
            _world.Tick();

            var grid = _world.Grid();
            Assert.IsTrue(grid.IsReachable(Infantry, float3.zero, new float3(30f, 0f, -20f)), "same bank");
            Assert.IsFalse(grid.IsReachable(Infantry, float3.zero, new float3(0f, 0f, 20f)), "across the river");
            Assert.IsTrue(grid.IsReachable(Ship, new float3(-50f, 0f, 10f), new float3(50f, 0f, 10f)), "along it");

            _world.SpawnNavArea(new float3(0f, 0f, 10f), new float2(6f, 12f), NavAreaKind.Deck);
            _world.Tick();

            Assert.IsTrue(_world.Grid().IsReachable(Infantry, float3.zero, new float3(0f, 0f, 20f)), "over the bridge");
        }

        [Test]
        public void GoalInTheRiver_SnapsToTheBankTheUnitIsOn()
        {
            SpawnRiver(10f);
            _world.Tick();
            var unit = _world.SpawnUnit(1, new float3(0f, 0f, -10f));

            // The far bank at z 15 is nearer this goal than the near one at z 5.
            _world.MoveTo(unit, new float3(0f, 0f, 14f));
            _world.Run(6f);

            var position = _world.GetPosition(unit);
            Assert.Less(position.z, 5f, "stays on its own bank");
            Assert.Greater(position.z, 3f, "drove up to the water");
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(unit), "and stopped there");
        }

        [Test]
        public void GoalFarAcrossTheWater_LeavesTheUnitWhereItIs()
        {
            SpawnRiver(90f);
            _world.Tick();
            var start = new float3(0f, 0f, -10f);
            var unit = _world.SpawnUnit(1, start);

            _world.MoveTo(unit, new float3(0f, 0f, 97f));
            _world.Run(1f);

            var moved = math.distance(_world.GetPosition(unit).xz, start.xz);
            Assert.Less(moved, 0.5f, "no ground near the goal to head for");
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(unit), "the move ends at once");
        }

        [Test]
        public void GoalInsideABuilding_StopsAtNearestOpenGround()
        {
            var building = new float3(10f, 0f, 0f);
            _world.SpawnBuilding(2, building, new float2(6f, 6f));
            var unit = _world.SpawnUnit(1, float3.zero);

            _world.EntityManager.SetComponentEnabled<Selected>(unit, true);
            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Position = building });
            _world.Run(6f);

            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(unit));
            Assert.IsTrue(_world.Grid().IsWalkable(_world.GetPosition(unit)));
            Assert.Less(math.distance(_world.GetPosition(unit).xz, building.xz), 5f);
        }

        [Test]
        public void BuildingPlaced_RePlansOnlyThePathsPassingIt()
        {
            var far = Drive(new float3(-50f, 0f, -50f), new float3(50f, 0f, -50f));
            var crossed = Drive(new float3(-50f, 0f, 50f), new float3(50f, 0f, 50f));
            _world.Run(0.2f);
            Mark(far);
            Mark(crossed);
            var version = _world.Grid().Version;

            _world.SpawnBuilding(2, new float3(0f, 0f, 50f), new float2(6f, 6f));
            _world.Tick();

            Assert.Greater(_world.Grid().Version, version);
            var kept = _world.Get<PathState>(far);
            Assert.AreEqual(PathStatus.Ready, kept.Status);
            Assert.AreEqual(_world.Grid().Version, kept.GridVersion, "revalidated on the new grid");
            Assert.AreEqual(1f, kept.Goal.y, "kept, not searched again");
            Assert.AreEqual(0f, _world.Get<PathState>(crossed).Goal.y, "searched again round the building");
            Assert.Greater(_world.EntityManager.GetBuffer<PathWaypoint>(crossed).Length, 1, "with a corner");
        }

        private Entity Drive(float3 start, float3 goal)
        {
            var unit = _world.SpawnUnit(1, start);
            _world.MoveTo(unit, goal);
            return unit;
        }

        // A search writes the goal it planned for, so a mark on its unused height shows whether one ran since.
        private void Mark(Entity unit)
        {
            var path = _world.Get<PathState>(unit);
            Assert.AreEqual(PathStatus.Ready, path.Status);
            path.Goal.y = 1f;
            _world.EntityManager.SetComponentData(unit, path);
        }

        [Test]
        public void LongHull_NoseAtTheEdge_IsInReach_ThoughItsCentreIsFar()
        {
            var centre = new float2(8f + TruckRadius + 0.5f + HalfSegment, 0f);
            var axis = new float2(HalfSegment, 0f);

            Assert.IsTrue(ReachMath.InReach(Yard, centre, axis, TruckRadius, 2f), "nose at the edge");
            Assert.IsFalse(ReachMath.InReach(Yard, centre + new float2(6f, 0f), axis, TruckRadius, 2f), "well back");
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
            var supplies = _world.CreateSupplies();
            var dropOff = _world.SpawnBuilding(1, new float3(-30f, 0f, 0f), new float2(16f, 16f));
            _world.EntityManager.AddComponent<ResourceDropOff>(dropOff);
            var node = _world.SpawnNode(new float3(10f, 0f, 0f), supplies, 100);
            var truck = _world.MakeHarvester(SpawnTruck(new float3(-8f, 0f, 0f)), 10, 10f);
            var footprint = new NavBox(new float3(-30f, 0f, 0f), quaternion.identity, new float2(16f, 16f));
            _world.Order(truck, OrderType.Gather, node);

            var closest = float.MaxValue;
            var reversed = false;
            for (var t = 0f; t < 60f && _world.GetResources(1, supplies) < 20; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var transform = _world.Get<LocalTransform>(truck);
                var axis = _world.Get<NavAgent>(truck).SegmentAxis(transform.Rotation);
                closest = math.min(closest, footprint.Gap(transform.Position.xz - axis, transform.Position.xz + axis));
                // Backing off a corner it noses into is the wall reflex, not a reverse leg, so only the legs count.
                var drive = _world.Get<LocomotionState>(truck);
                reversed |= drive.Reverse || drive.InThreePointTurn;
            }

            Assert.GreaterOrEqual(_world.GetResources(1, supplies), 20, "two loads delivered");
            Assert.GreaterOrEqual(closest, TruckRadius - 0.05f, $"the hull never entered the footprint ({closest:F2} m)");
            Assert.IsFalse(reversed, "the round trips drive forward");
        }

        [Test]
        public void Builder_WalledOffFromItsSite_GivesUpTheOrder()
        {
            // A ring of walls round the site, 4 m clear of it, with no way in.
            var site = _world.SpawnBuilding(1, new float3(20f, 0f, 0f), new float2(4f, 4f), complete: false,
                buildTime: 60f);
            _world.SpawnObstacle(new float3(20f, 0f, 7f), new float2(16f, 2f));
            _world.SpawnObstacle(new float3(20f, 0f, -7f), new float2(16f, 2f));
            _world.SpawnObstacle(new float3(13f, 0f, 0f), new float2(2f, 12f));
            _world.SpawnObstacle(new float3(27f, 0f, 0f), new float2(2f, 12f));
            var dozer = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero));
            _world.Order(dozer, OrderType.Build, site);

            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<ActiveOrder>(dozer), 20f), "the order finished");
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(dozer), "and the walk with it");
            Assert.AreEqual(0f, _world.Get<ConstructionProgress>(site).Value, "nothing was built");
        }

        private Entity SpawnTruck(float3 position)
        {
            var truck = _world.SpawnUnit(1, position, speed: 8f, radius: TruckRadius);
            _world.EntityManager.SetComponentData(truck, LocalTransform.FromPositionRotation(position,
                quaternion.RotateY(math.PI * 0.5f)));
            _world.EntityManager.SetComponentData(truck, new NavAgent
            {
                Radius = TruckRadius, HullLength = TruckLength, Spacing = UnitAuthoring.DefaultSpacing,
                Chassis = new Chassis { Wheelbase = 4f, RearAxle = 1.2f, TurnRadius = 10f, ReverseRun = 6f },
            });
            var writer = new EntityManagerWriter(_world.EntityManager, truck);
            LocomotionAuthoring.Add(ref writer, new Locomotion
            {
                Acceleration = 3f, Braking = 6f, TurnRate = math.radians(35f), SteerRate = math.radians(45f),
                ReverseSpeed = 3f,
            });
            return truck;
        }
    }
}
