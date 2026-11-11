using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation.Grid
{
    /// <summary>
    /// The nav grid: obstacle boxes, rebuilds when buildings change and the signed distance field.
    /// </summary>
    public class NavGridTests : SimulationFixture
    {
        private static readonly NavAgent Infantry = new() { Radius = 0.5f, SceneryCrushLimit = NavAccess.AllScenery };

        // Stands in for fog tags, which move enemy buildings between archetypes without changing their footprints.
        private struct ArchetypeTag : IComponentData
        {
        }

        [SetUp]
        public void Prepare()
        {
            World.AddAgentPrefabs(Infantry);
        }

        [Test]
        public void DiagonalBox_CoversItsCentreButNotTheCornersOfItsBounds()
        {
            var box = new NavBox(float3.zero, quaternion.RotateY(math.PI * 0.25f), new float2(10f, 2f));
            var corner = box.Extents * 0.95f;

            Assert.IsFalse(box.IsAxisAligned);
            Assert.IsTrue(box.Contains(float2.zero, 0f));
            Assert.IsFalse(box.Contains(new float2(corner.x, -corner.y), 0f), "the empty corner of the world box");
            Assert.IsTrue(box.Contains(new float2(3f, -3f), 0f) || box.Contains(new float2(3f, 3f), 0f), "along its length");
        }

        [Test]
        public void Grid_KeepsItsVersion_WhenABuildingOnlyChangesArchetype_AndRebuildsWhenItMoves()
        {
            var site = new float3(20f, 0f, 20f);
            var building = World.SpawnBuilding(2, site, new float2(4f, 4f));
            World.Tick();
            var version = World.Grid().Version;

            World.EntityManager.AddComponent<ArchetypeTag>(building);
            World.Tick();
            Assert.AreEqual(version, World.Grid().Version, "same footprints, so paths stay valid");

            var moved = site + new float3(10f, 0f, 0f);
            var transform = World.Get<LocalTransform>(building);
            World.Set(building, transform.WithPosition(moved));
            World.EntityManager.RemoveComponent<ArchetypeTag>(building);
            World.Tick();

            var grid = World.Grid();
            Assert.Greater(grid.Version, version);
            Assert.IsTrue(grid.IsWalkable(site));
            Assert.IsFalse(grid.IsWalkable(moved));
        }

        private float GroundField(float x, float z)
        {
            var grid = World.Grid();
            return Infantry.Access(grid).Distance(grid.Grid.Cell(new float3(x, 0f, z)));
        }

        [Test]
        public void Field_IsNegativeInsideWalls_AboutZeroOnTheirEdge_AndFollowsObstacles()
        {
            var obstacle = World.SpawnObstacle(new float3(0.5f, 0f, 0.5f), new float2(1f, 1f));
            World.SpawnObstacle(new float3(50f, 0f, 50f), new float2(20f, 20f));
            World.Tick();

            Assert.AreEqual(-0.5f, GroundField(0.5f, 0.5f), 1e-4f, "the closed cell");
            Assert.AreEqual(0.5f, GroundField(-0.5f, 0.5f), 1e-4f, "beside it");
            Assert.AreEqual(math.SQRT2 - 0.5f, GroundField(-0.5f, -0.5f), 1e-4f, "diagonally below it");
            Assert.AreEqual(2.5f, GroundField(-2.5f, 0.5f), 1e-4f, "three cells out");
            Assert.AreEqual(-NavField.MaxDistance, GroundField(50f, 50f), "deep inside a building, capped");
            var access = Infantry.Access(World.Grid());
            Assert.AreEqual(0f, access.Distance(new float2(0f, 0.5f)), 1e-4f, "on its edge");
            MathAssert.Near(new float2(-1f, 0f), access.Normal(new float2(-0.2f, 0.5f)));

            World.EntityManager.DestroyEntity(obstacle);
            World.Tick();

            Assert.AreEqual(NavField.MaxDistance, GroundField(0.5f, 0.5f), "reopened once the obstacle is gone");
            Assert.AreEqual(NavField.MaxDistance, GroundField(-0.5f, -0.5f));
        }

        [Test]
        public void GapExactlyTwoRadiiWide_LetsThatRadiusThrough()
        {
            // A wall along x with a three-cell gap from x -1 to 2, so the middle cell is 1.5 m from each side.
            World.SpawnObstacle(new float3(-51f, 0f, 0.5f), new float2(100f, 1f));
            World.SpawnObstacle(new float3(52f, 0f, 0.5f), new float2(100f, 1f));
            var exact = new NavAgent { Radius = 1.5f };
            var wider = new NavAgent { Radius = 1.6f };
            World.AddAgentPrefabs(exact, wider);
            World.Tick();

            var grid = World.Grid();
            var south = new float3(0.5f, 0f, -10f);
            var north = new float3(0.5f, 0f, 10f);
            Assert.IsTrue(exact.Access(grid).IsOpen(grid.Grid.Cell(new float3(0.5f, 0f, 0.5f))), "the gap's middle");
            Assert.IsTrue(grid.IsReachable(exact, south, north), "through the gap");
            Assert.IsFalse(grid.IsReachable(wider, south, north), "a hair wider can't pass");
        }
    }
}
