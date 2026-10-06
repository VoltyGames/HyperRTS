using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Terrain height: ground following, placement height, slope limits and fog line-of-sight occlusion.</summary>
    public class TerrainTests
    {
        private const byte Team1 = 1;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private static float Rolling(float2 p) => p.x * 0.2f + math.sin(p.y * 0.1f) * 2f;

        /// <summary>Rolling hills dip below 0, so keep the sea under them.</summary>
        private void CreateHills()
        {
            _world.CreateTerrain(Rolling);
            _world.ConfigureMap(waterLevel: -100f);
        }

        private static FogOfWar Fog(TestWorld world)
        {
            world.EntityManager.CompleteAllTrackedJobs();
            using var query = world.EntityManager.CreateEntityQuery(typeof(FogOfWar));
            return query.GetSingleton<FogOfWar>();
        }

        [Test]
        public void Units_FollowTheTerrain_WhileMoving()
        {
            CreateHills();
            var unit = _world.SpawnUnit(1, new float3(-10f, 0f, -5f));
            var goal = new float3(10f, 0f, 5f);
            _world.MoveTo(unit, goal);

            for (var t = 0f; t < 6f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var position = _world.PositionOf(unit);
                Assert.AreEqual(Rolling(position.xz), position.y, 0.05f, $"off the ground at {position}");
            }

            Assert.Less(math.distance(_world.PositionOf(unit).xz, goal.xz), 0.5f);
        }

        [Test]
        public void PlacedBuildings_SitOnTheTerrain()
        {
            CreateHills();
            var prefab = _world.MakePrefab(_world.SpawnBuilding(0, new float3(-90f, 0f, 90f), new float2(4f, 4f)));
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero), prefab);
            _world.Tick();

            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = new float3(6.3f, 0f, 0.2f),
            });
            _world.Tick();

            var sites = _world.All<BuildingTag>();
            Assert.AreEqual(1, sites.Length);
            Assert.AreEqual(new float3(6f, Rolling(new float2(6f, 0f)), 0f), _world.Get<LocalTransform>(sites[0]).Position);
        }

        [Test]
        public void SteepSlopes_BlockGround_WhenMaxSlopeIsSet()
        {
            // A 60-degree ramp east of x = 10, flat ground elsewhere.
            _world.CreateTerrain(p => p.x > 10f ? (p.x - 10f) * 1.8f : 0f);
            _world.ConfigureMap(waterLevel: 0f, maxSlope: 45f);
            _world.Tick();

            var grid = _world.Grid();
            Assert.IsTrue(grid.IsWalkable(float3.zero));
            Assert.IsFalse(grid.IsWalkable(new float3(20f, 0f, 0f)), "steeper than the limit");
        }

        [Test]
        public void Ridge_HidesTheGroundBehindIt()
        {
            // A 10 m ridge along z at x = 4..6, flat ground around it.
            _world.CreateTerrain(p => math.abs(p.x - 5f) < 1.5f ? 10f : 0f);
            _world.SpawnUnit(1, float3.zero);
            _world.Tick();

            var fog = Fog(_world);
            Assert.IsTrue(fog.IsVisible(new float3(3f, 0f, 0f), Team1), "in front of the ridge");
            Assert.IsTrue(fog.IsVisible(new float3(5f, 0f, 0f), Team1), "the ridge top");
            Assert.IsFalse(fog.IsVisible(new float3(9f, 0f, 0f), Team1), "behind the ridge");
            Assert.IsTrue(fog.IsVisible(new float3(0f, 0f, 9f), Team1), "open ground along the ridge");
        }

        [Test]
        public void FlatTerrain_SeesTheSameCircle_AsNoTerrain()
        {
            using var flat = new TestWorld();
            flat.CreateMatch(1, 2);
            flat.CreateTerrain(_ => 0f);
            var position = new float3(3.3f, 0f, -7.1f);
            flat.SpawnUnit(1, position);
            _world.SpawnUnit(1, position);
            flat.Tick();
            _world.Tick();

            var occluded = Fog(flat);
            var plain = Fog(_world);
            for (var i = 0; i < plain.Visible.Length; i++)
            {
                Assert.AreEqual(plain.Visible[i], occluded.Visible[i], $"cell {plain.Cell(i)}");
            }
        }
    }
}
