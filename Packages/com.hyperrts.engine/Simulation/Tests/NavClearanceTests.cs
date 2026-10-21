using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The clearance field, wide units keeping out of narrow gaps, and capsules held off walls.</summary>
    public class NavClearanceTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity SpawnObstacle(float3 center, float2 size)
        {
            var obstacle = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(obstacle, LocalTransform.FromPosition(center));
            var writer = new EntityManagerWriter(_world.EntityManager, obstacle);
            NavSetup.AddObstacle(ref writer, size);
            return obstacle;
        }

        private int GroundClearance(float x, float z)
        {
            var grid = _world.Grid();
            return grid.ClearanceAt((int)NavLayer.Ground, grid.WorldToCell(new float3(x, 0f, z)));
        }

        [Test]
        public void Clearance_CountsTheOpenSquareAboveEachCell_AndFollowsObstacles()
        {
            var obstacle = SpawnObstacle(new float3(0.5f, 0f, 0.5f), new float2(1f, 1f));
            _world.Tick();

            Assert.AreEqual(0, GroundClearance(0.5f, 0.5f), "the closed cell");
            Assert.AreEqual(1, GroundClearance(-0.5f, -0.5f), "diagonally below it");
            Assert.AreEqual(2, GroundClearance(-1.5f, -1.5f));
            Assert.AreEqual(3, GroundClearance(-2.5f, -1.5f), "its 3 by 3 square stops a column short");
            Assert.AreEqual(6, GroundClearance(-5.5f, -0.5f), "a wider square would reach the obstacle");

            _world.EntityManager.DestroyEntity(obstacle);
            _world.Tick();

            Assert.Greater(GroundClearance(0.5f, 0.5f), 50, "reopened once the obstacle is gone");
            Assert.Greater(GroundClearance(-0.5f, -0.5f), 50);
        }

        [Test]
        public void WideUnit_TakesTheWideGap_WhileASmallUnitSlipsThroughTheNarrowOne()
        {
            // A wall along Z with a one-cell gap at z 0 to 1 and a three-cell gap at z 10 to 13.
            SpawnObstacle(new float3(0.5f, 0f, -10f), new float2(1f, 20f));
            SpawnObstacle(new float3(0.5f, 0f, 5.5f), new float2(1f, 9f));
            SpawnObstacle(new float3(0.5f, 0f, 22f), new float2(1f, 18f));
            _world.Tick();

            var small = _world.SpawnUnit(1, new float3(-6f, 0f, 0.5f), radius: 0.4f);
            var wide = _world.SpawnUnit(1, new float3(-6f, 0f, 5f), radius: 1f);
            var goal = new float3(6f, 0f, 0.5f);
            var wideGoal = new float3(6f, 0f, 5f);
            _world.MoveTo(small, goal);
            _world.MoveTo(wide, wideGoal);

            var smallWidest = 0f;
            var wideWidest = 0f;
            for (var t = 0f; t < 15f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                smallWidest = math.max(smallWidest, _world.GetPosition(small).z);
                wideWidest = math.max(wideWidest, _world.GetPosition(wide).z);
                Assert.IsTrue(_world.Grid().IsWalkable(_world.GetPosition(wide)), "the wide unit stays off the wall");
            }

            Assert.Less(smallWidest, 3f, "the small unit took the narrow gap");
            Assert.Greater(wideWidest, 10f, "the wide unit went round by the wide gap, though the narrow one is shorter");
            Assert.Less(math.distance(_world.GetPosition(wide).xz, wideGoal.xz), 1f);
            Assert.Less(math.distance(_world.GetPosition(small).xz, goal.xz), 1f);
        }

        [Test]
        public void Constrain_KeepsTheCapsuleNoseOffAWall_AndSlidesAlongIt()
        {
            SpawnObstacle(new float3(2.5f, 0f, 0f), new float2(1f, 20f));
            _world.Tick();
            var access = new NavAgent { Radius = 0.5f, HullLength = 4f }.Access(_world.Grid());
            var capsule = new NavCapsule(access, new float2(1.5f, 0f));
            var from = new float2(-0.5f, 0.5f);

            Assert.AreEqual(from, capsule.Constrain(from, new float2(0.2f, 0.5f)),
                "the centre is open, but the nose would enter the wall");
            Assert.AreEqual(new float2(-0.5f, 1.5f), capsule.Constrain(from, new float2(0.2f, 1.5f)),
                "a slanted move slides along it");
            Assert.AreEqual(new float2(-0.6f, 0.5f), capsule.Constrain(from, new float2(-0.6f, 0.5f)),
                "backing away is free");
        }

        [Test]
        public void CutsCornerBetweenOpen_MatchesTheFullSideSquareCheck()
        {
            SpawnObstacle(new float3(0.5f, 0f, 0.5f), new float2(1f, 1f));
            SpawnObstacle(new float3(3f, 0f, -2.5f), new float2(2f, 1f));
            _world.Tick();
            var grid = _world.Grid();
            var center = grid.WorldToCell(float3.zero);
            grid.Scenery[grid.Index(center + new int2(-3, 2))] = 5;

            var cuts = 0;
            for (var size = 1; size <= 3; size++)
            {
                var access = new NavAccess(grid, NavLayer.Ground, size * grid.CellSize * 0.5f, 2);
                Assert.AreEqual(size, access.Footprint);
                for (var y = -6; y <= 6; y++)
                {
                    for (var x = -6; x <= 6; x++)
                    {
                        cuts += CompareDiagonals(access, center + new int2(x, y));
                    }
                }
            }

            grid.Scenery[grid.Index(center + new int2(-3, 2))] = 0;
            Assert.Greater(cuts, 0, "some steps near the obstacles cut a corner");
        }

        private static int CompareDiagonals(in NavAccess access, int2 from)
        {
            var cuts = 0;
            for (var dy = -1; dy <= 1; dy += 2)
            {
                for (var dx = -1; dx <= 1; dx += 2)
                {
                    var to = from + new int2(dx, dy);
                    if (!access.IsAnchorOpen(from) || !access.IsAnchorOpen(to))
                    {
                        continue;
                    }

                    var expected = access.CutsCorner(from, to, access.Footprint);
                    Assert.AreEqual(expected, access.CutsCornerBetweenOpen(from, to), $"{from} to {to}");
                    cuts += expected ? 1 : 0;
                }
            }

            return cuts;
        }
    }
}
