using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// Nav regions and changed areas: reachability per layer and width, goals snapping to the start's side, and grid
    /// changes re-planning only the paths that pass near them.
    /// </summary>
    public class NavRegionTests
    {
        private static readonly NavAgent Infantry = new() { Radius = 0.5f };
        private static readonly NavAgent Tank = new() { Radius = 1.85f };
        private static readonly NavAgent Ship = new() { Radius = 0.5f, Layer = NavLayer.Naval };

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.AddAgentPrefabs(Infantry, Tank, Ship);
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
        public void GapOneCellWide_PassesInfantry_ButNotATank()
        {
            _world.SpawnNavArea(new float3(-50f, 0f, 0f), new float2(100f, 2f), NavAreaKind.Blocked);
            _world.SpawnNavArea(new float3(50.5f, 0f, 0f), new float2(99f, 2f), NavAreaKind.Blocked);
            _world.Tick();

            var grid = _world.Grid();
            var south = new float3(0.5f, 0f, -10f);
            var north = new float3(0.5f, 0f, 10f);
            Assert.IsTrue(grid.IsReachable(Infantry, south, north));
            Assert.IsFalse(grid.IsReachable(Tank, south, north));
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
    }
}
