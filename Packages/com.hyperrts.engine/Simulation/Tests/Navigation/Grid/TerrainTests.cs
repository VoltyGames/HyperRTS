using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation.Grid
{
    /// <summary>Terrain height: ground following, placement height, slope limits and fog line-of-sight occlusion.</summary>
    public class TerrainTests : SimulationFixture
    {
        private const byte Team1 = 1;

        private static float Rolling(float2 p) => p.x * 0.2f + math.sin(p.y * 0.1f) * 2f;

        private void CreateHills() => World.CreateTerrain(Rolling);

        [Test]
        public void Units_FollowTheTerrain_WhileMoving()
        {
            CreateHills();
            var unit = World.SpawnUnit(1, new float3(-10f, 0f, -5f));
            var goal = new float3(10f, 0f, 5f);
            World.MoveTo(unit, goal);

            for (var t = 0f; t < 6f; t += TestWorld.FrameTime)
            {
                World.Tick();
                var position = World.GetPosition(unit);
                Assert.AreEqual(Rolling(position.xz), position.y, 0.05f, $"off the ground at {position}");
            }

            Assert.Less(math.distance(World.GetPosition(unit).xz, goal.xz), 0.5f);
        }

        [Test]
        public void PlacedBuildings_SitOnTheTerrain()
        {
            CreateHills();
            var prefab = World.MakePrefab(World.SpawnBuilding(0, new float3(-90f, 0f, 90f), new float2(4f, 4f)));
            var builder = World.MakeBuilder(World.SpawnUnit(1, float3.zero), prefab);
            World.Tick();

            World.Command(1, new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = new float3(6.3f, 0f, 0.2f),
            });
            World.Tick();

            var sites = World.All<BuildingTag>();
            Assert.AreEqual(1, sites.Length);
            Assert.AreEqual(new float3(6f, Rolling(new float2(6f, 0f)), 0f), World.Get<LocalTransform>(sites[0]).Position);
        }

        [Test]
        public void SteepSlopes_BlockGround_WhenMaxSlopeIsSet()
        {
            // A 60-degree ramp east of x = 10, flat ground elsewhere.
            World.CreateTerrain(p => p.x > 10f ? (p.x - 10f) * 1.8f : 0f);
            World.ConfigureMap(maxSlope: 45f);
            World.Tick();

            var grid = World.Grid();
            Assert.IsTrue(grid.IsWalkable(float3.zero));
            Assert.IsFalse(grid.IsWalkable(new float3(20f, 0f, 0f)), "steeper than the limit");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TerrainBelowWaterLevel_FloodsOnlyWhenFloodTerrainIsOn(bool flood)
        {
            // A 10 m deep basin east of x = 4, flat ground elsewhere.
            World.CreateTerrain(p => p.x > 4f ? -10f : 0f);
            World.ConfigureMap(waterLevel: -1f, floodTerrain: flood);
            World.SpawnUnit(1, float3.zero);
            World.Tick();

            var basin = new float3(8f, 0f, 0f);
            var grid = World.Grid();
            Assert.IsTrue(grid.IsWalkable(float3.zero), "dry ground stays land");
            Assert.AreEqual(!flood, grid.IsWalkable(basin, NavLayer.Ground), "basin floor is land");
            Assert.AreEqual(flood, grid.IsWalkable(basin, NavLayer.Naval), "basin is water");
            // The near shore hides the deep floor; only a water surface rises into view.
            Assert.AreEqual(flood, World.Fog().IsVisible(new float3(9f, 0f, 0f), Team1, false), "fog sees the water surface");
        }

        [Test]
        public void TerrainStreamingInLate_RebuildsTheNavGrid()
        {
            World.ConfigureMap(waterLevel: -1f, floodTerrain: true);
            World.Tick();
            var basin = new float3(8f, 0f, 0f);
            Assert.IsTrue(World.Grid().IsWalkable(basin), "flat ground before the terrain arrives");
            var version = World.Grid().Version;

            World.CreateTerrain(p => p.x > 4f ? -10f : 0f);
            World.Tick();

            var grid = World.Grid();
            Assert.Greater(grid.Version, version, "paths revalidate against the new grid");
            Assert.IsTrue(grid.IsWalkable(basin, NavLayer.Naval), "the basin floods once the terrain is in");

            World.ConfigureMap(waterLevel: -20f, floodTerrain: true);
            World.Tick();
            Assert.IsTrue(World.Grid().IsWalkable(basin), "a lower water level drains it again");
        }

        [TestCase(false, TestName = "Ridge_HidesTheGroundBehindIt")]
        [TestCase(true, TestName = "TallViewer_SeesOverTheRidge")]
        public void Ridge_HidesTheGroundBehindIt_FromAViewerBelowItsTop(bool tall)
        {
            // A 10 m ridge along z at x 4 to 6, flat ground around it.
            World.CreateTerrain(p => math.abs(p.x - 5f) < 1.5f ? 10f : 0f);
            var viewer = World.SpawnUnit(1, float3.zero);
            if (tall)
            {
                World.Set(viewer, new VisionRange { Value = 10f, EyeHeight = 50f });
            }

            World.Tick();

            var fog = World.Fog();
            Assert.IsTrue(fog.IsVisible(new float3(3f, 0f, 0f), Team1, false), "in front of the ridge");
            Assert.IsTrue(fog.IsVisible(new float3(5f, 0f, 0f), Team1, false), "the ridge top");
            Assert.AreEqual(tall, fog.IsVisible(new float3(9f, 0f, 0f), Team1, false), "behind the ridge");
            Assert.IsTrue(fog.IsVisible(new float3(0f, 0f, 9f), Team1, false), "open ground along the ridge");
        }

        [Test]
        public void FlatTerrain_SeesTheSameCircle_AsNoTerrain()
        {
            using var flat = new TestWorld();
            flat.CreateMatch(1, 2);
            flat.CreateTerrain(_ => 0f);
            var position = new float3(3.3f, 0f, -7.1f);
            flat.SpawnUnit(1, position);
            World.SpawnUnit(1, position);
            flat.Tick();
            World.Tick();

            var occluded = flat.Fog();
            var plain = World.Fog();
            for (var i = 0; i < plain.Visible.Length; i++)
            {
                Assert.AreEqual(plain.Visible[i], occluded.Visible[i], $"cell {plain.Grid.Cell(i)}");
            }
        }
    }
}
