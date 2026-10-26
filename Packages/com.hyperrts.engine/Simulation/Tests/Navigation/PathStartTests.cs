using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>
    /// Where a unit standing in a blocked cell starts its path: open ground it can squeeze to, the goal's side
    /// first, never across water.
    /// </summary>
    public class PathStartTests
    {
        private const float DozerRadius = 2f;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, 200f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void DozerInANarrowSlot_LeavesTowardTheGoal_NotIntoTheNearerPocket()
        {
            // A 3 m slot along x from -10 to 10, a closed pocket off its west end, open ground past its east end.
            _world.SpawnObstacle(new float3(0f, 0f, 3.5f), new float2(20f, 4f));
            _world.SpawnObstacle(new float3(0f, 0f, -3.5f), new float2(20f, 4f));
            _world.SpawnObstacle(new float3(-17f, 0f, 0f), new float2(2f, 15f));
            _world.SpawnObstacle(new float3(-13.5f, 0f, 6.5f), new float2(7f, 2f));
            _world.SpawnObstacle(new float3(-13.5f, 0f, -6.5f), new float2(7f, 2f));
            _world.Tick();
            var dozer = _world.SpawnUnit(1, new float3(-7f, 0f, 0f), radius: DozerRadius);
            _world.Tick();
            var goal = new float3(30f, 0f, 0f);

            var query = GridPathfinder.Prepare(Access(dozer), new float3(-7f, 0f, 0f), goal);
            Assert.AreNotEqual(PathRoute.Stay, query.Route, "the goal isn't snapped into the pocket");
            Assert.Greater(query.Origin.x, 9f, "the path starts past the slot's east end");

            _world.MoveTo(dozer, goal);
            _world.Tick();
            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<MoveDestination>(dozer), 30f), "the move ended");
            var end = _world.Get<LocalTransform>(dozer).Position;
            var reason = _world.Get<Arrival>(dozer).Reason;
            Assert.Less(math.distance(end.xz, goal.xz), 3f, $"at the goal, not stopped at {end} ({reason})");
        }

        [Test]
        public void BlockedOnARiverbank_StartsOnItsOwnBank_NotAcrossTheWater()
        {
            // A 4 m river at z 8 to 12, and a building that leaves only a 2 m strip between it and the water.
            _world.SpawnNavArea(new float3(0f, 0f, 10f), new float2(200f, 4f), NavAreaKind.Water);
            _world.SpawnObstacle(new float3(0f, 0f, 4f), new float2(20f, 4f));
            _world.Tick();
            var start = new float3(0f, 0f, 7f);
            var unit = _world.SpawnUnit(1, start, radius: DozerRadius);
            _world.Tick();
            var access = Access(unit);
            Assert.IsFalse(access.IsOpen(_world.Grid().WorldToCell(start)), "the unit stands in a blocked cell");

            var query = GridPathfinder.Prepare(access, start, new float3(0f, 0f, 30f));

            Assert.Less(_world.Grid().CellCenter(query.From).z, 8f, "the path starts on the near bank");
            Assert.Less(query.End.z, 8f, "with no bridge, the goal snaps to the near bank too");
        }

        private NavAccess Access(Entity unit) => _world.Get<NavAgent>(unit).Access(_world.Grid());
    }
}
