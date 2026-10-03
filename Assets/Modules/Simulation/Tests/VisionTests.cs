using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Fog-of-war grid: per-team visibility, explored memory and the fog-off switch.</summary>
    public class VisionTests
    {
        private const byte Team1 = 1;
        private const byte Team2 = 2;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(Team1, Team2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private FogOfWar Fog()
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            using var query = _world.EntityManager.CreateEntityQuery(typeof(FogOfWar));
            return query.GetSingleton<FogOfWar>();
        }

        [Test]
        public void Fog_RevealsVisionRange_OnlyForOwnersTeam()
        {
            _world.SpawnUnit(1, float3.zero);

            _world.Tick();
            var fog = Fog();

            Assert.IsTrue(fog.IsVisible(new float3(5f, 0f, 0f), Team1));
            Assert.IsFalse(fog.IsVisible(new float3(5f, 0f, 0f), Team2), "enemies don't share vision");
            Assert.IsFalse(fog.IsVisible(new float3(30f, 0f, 0f), Team1), "beyond the vision range");
        }

        [Test]
        public void Fog_KeepsExploredCells_AfterTheUnitLeaves()
        {
            var scout = _world.SpawnUnit(1, float3.zero);
            _world.Tick();
            var version = Fog().Version;

            _world.EntityManager.SetComponentData(scout, LocalTransform.FromPosition(new float3(50f, 0f, 0f)));
            _world.Run(0.5f);
            var fog = Fog();

            Assert.IsFalse(fog.IsVisible(float3.zero, Team1));
            Assert.IsTrue(fog.IsExplored(float3.zero, Team1));
            Assert.IsTrue(fog.IsVisible(new float3(50f, 0f, 0f), Team1));
            Assert.Greater(fog.Version, version, "restamps bump the version");
        }

        [Test]
        public void FogDisabled_EverythingVisible()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(MapSettings));
            var settings = query.GetSingleton<MapSettings>();
            settings.FogOfWar = false;
            query.SetSingleton(settings);

            _world.Tick();
            var fog = Fog();

            Assert.IsTrue(fog.IsVisible(new float3(90f, 0f, -90f), Team1));
            Assert.IsTrue(fog.IsVisible(new float3(-90f, 0f, 90f), Team2));
            Assert.IsTrue(fog.IsExplored(float3.zero, Team2));
        }
    }
}
