using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The distance field, wide units keeping out of narrow gaps, and capsules held off walls.</summary>
    public class NavClearanceTests
    {
        private static readonly NavAgent Infantry = new() { Radius = 0.5f };
        private static readonly NavAgent Wide = new() { Radius = 1f };

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.AddAgentPrefabs(Infantry, Wide);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private float GroundField(float x, float z)
        {
            var grid = _world.Grid();
            return Infantry.Access(grid).Distance(grid.WorldToCell(new float3(x, 0f, z)));
        }

        [Test]
        public void Field_IsNegativeInsideWalls_AboutZeroOnTheirEdge_AndFollowsObstacles()
        {
            var obstacle = _world.SpawnObstacle(new float3(0.5f, 0f, 0.5f), new float2(1f, 1f));
            _world.Tick();

            Assert.AreEqual(-0.5f, GroundField(0.5f, 0.5f), 1e-4f, "the closed cell");
            Assert.AreEqual(0.5f, GroundField(-0.5f, 0.5f), 1e-4f, "beside it");
            Assert.AreEqual(math.SQRT2 - 0.5f, GroundField(-0.5f, -0.5f), 1e-4f, "diagonally below it");
            Assert.AreEqual(2.5f, GroundField(-2.5f, 0.5f), 1e-4f, "three cells out");
            var access = Infantry.Access(_world.Grid());
            Assert.AreEqual(0f, access.Distance(new float2(0f, 0.5f)), 1e-4f, "on its edge");
            MathAssert.Near(new float2(-1f, 0f), access.Normal(new float2(-0.2f, 0.5f)));

            _world.EntityManager.DestroyEntity(obstacle);
            _world.Tick();

            Assert.AreEqual(NavField.Cap, GroundField(0.5f, 0.5f), "reopened once the obstacle is gone");
            Assert.AreEqual(NavField.Cap, GroundField(-0.5f, -0.5f));
        }

        [Test]
        public void WideUnit_TakesTheWideGap_WhileASmallUnitSlipsThroughTheNarrowOne()
        {
            // A wall along Z with a one-cell gap at z 0 to 1 and a three-cell gap at z 10 to 13.
            _world.SpawnObstacle(new float3(0.5f, 0f, -10f), new float2(1f, 20f));
            _world.SpawnObstacle(new float3(0.5f, 0f, 5.5f), new float2(1f, 9f));
            _world.SpawnObstacle(new float3(0.5f, 0f, 22f), new float2(1f, 18f));
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
            _world.SpawnObstacle(new float3(2.5f, 0f, 0f), new float2(1f, 20f));
            _world.Tick();
            var access = new NavAgent { Radius = 0.5f, HullLength = 4f }.Access(_world.Grid());
            var capsule = new NavCapsule(access, new float2(1.5f, 0f));
            var from = new float2(-0.5f, 0.5f);

            Assert.Less(math.distance(from, capsule.Constrain(from, new float2(0.2f, 0.5f))), 1e-3f,
                "the centre is open, but the nose would enter the wall");
            var slid = capsule.Constrain(from, new float2(0.2f, 1.5f));
            Assert.Less(math.distance(new float2(-0.5f, 1.5f), slid), 1e-3f, "a slanted move slides along it");
            Assert.AreEqual(new float2(-0.6f, 0.5f), capsule.Constrain(from, new float2(-0.6f, 0.5f)),
                "backing away is free");
        }

        [Test]
        public void Capsule_PushedIntoADiagonalWall_SlidesAlongIt()
        {
            // A wall along x = z, stamped as a staircase of cells.
            var wall = _world.SpawnObstacle(float3.zero, new float2(1f, 40f));
            _world.EntityManager.SetComponentData(wall,
                LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(math.PI * 0.25f)));
            _world.Tick();
            // Two cells wide, so it rides the step corners rather than sitting in the notches between them.
            var capsule = new NavCapsule(Wide.Access(_world.Grid()), float2.zero);
            var along = math.normalize(new float2(1f, 1f));
            var move = 0.2f * new float2(math.cos(math.radians(20f)), math.sin(math.radians(20f)));

            var position = new float2(-4f, 4.3f);
            var start = math.dot(position, along);
            var deepest = 0f;
            for (var i = 0; i < 100; i++)
            {
                position = capsule.Constrain(position, position + move);
                deepest = math.max(deepest, capsule.Depth(position, float2.zero));
            }

            // Pushed 25 degrees into it, nearly all the move along it is kept; sliding on one axis keeps under 85 %.
            var ideal = 100f * math.dot(move, along);
            Assert.Greater(math.dot(position, along) - start, ideal * 0.9f, $"slid along it, ending at {position}");
            Assert.Less(position.x, position.y, "on its own side");
            Assert.Less(deepest, 0.05f, "and never sank into it");
        }
    }
}
