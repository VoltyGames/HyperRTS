using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Navigation.Grid;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Buildings.Construction
{
    /// <summary>
    /// A building site removes the open scenery its footprint overlaps and the remains on it, quietly.
    /// </summary>
    public class SiteClearingTests
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

        private EntityManager Manager => _world.EntityManager;

        private Entity SpawnScenery(float3 position, Entity remains = default, float2 block = default)
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponentData(entity, LocalTransform.FromPosition(position));
            var writer = new EntityWriter(Manager, entity);
            DestructibleAuthoring.AddScenery(writer, 50f, 1, remains, block,
                SceneryId.At(position, quaternion.identity));
            return entity;
        }

        private Entity SpawnRuin(float3 position)
        {
            var ruin = Manager.CreateEntity();
            Manager.AddComponentData(ruin, LocalTransform.FromPosition(position));
            Manager.AddComponentData(ruin, SceneryId.At(position, quaternion.identity));
            return ruin;
        }

        private Entity SpawnTurnedSite(float yaw)
        {
            var site = _world.SpawnBuilding(1, float3.zero, new float2(10f, 2f), complete: false);
            Manager.SetComponentData(site, LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(yaw)));
            return site;
        }

        [Test]
        public void Site_ClearsSceneryCrossingItsEdgeAndRuinsOnIt_AndLeavesNoRemains()
        {
            var remains = _world.MakePrefab(Manager.CreateEntity(typeof(LocalTransform), typeof(Decay)));
            var fence = SpawnScenery(new float3(5.3f, 0f, 0f), remains);
            var ruin = SpawnRuin(new float3(-3f, 0f, 0f));
            var outside = SpawnScenery(new float3(7f, 0f, 0f));
            _world.SpawnBuilding(1, float3.zero, new float2(10f, 2f), complete: false);

            _world.Run(0.5f);

            Assert.IsFalse(Manager.Exists(fence), "a fence whose end reaches over the edge is cleared");
            Assert.IsFalse(Manager.Exists(ruin), "so are remains lying on the site");
            Assert.IsTrue(Manager.Exists(outside), "scenery clear of the footprint stands");
            using var spawned = Manager.CreateEntityQuery(ComponentType.ReadOnly<Decay>());
            Assert.AreEqual(0, spawned.CalculateEntityCount(), "a cleared piece leaves no remains");
        }

        [Test]
        public void TurnedSite_ClearsAlongItsTurnedBox_NotItsWorldBox()
        {
            var along = SpawnScenery(new float3(3f, 0f, -3f));
            var across = SpawnScenery(new float3(3f, 0f, 3f));
            SpawnTurnedSite(math.PI / 4f);

            _world.Run(0.5f);

            Assert.IsFalse(Manager.Exists(along), "scenery along the turned length is cleared");
            Assert.IsTrue(Manager.Exists(across), "scenery off its side stands");
        }

        [Test]
        public void TreeBesideTheSite_GoesWhenItsCanopyReachesOver()
        {
            var overhanging = SpawnScenery(new float3(0f, 0f, 3f));
            var clear = SpawnScenery(new float3(0f, 0f, -5f));
            foreach (var tree in new[] { overhanging, clear })
            {
                var writer = new EntityWriter(Manager, tree);
                ToppleAuthoring.AddTopple(writer, new float3(0f, 1f, 0f), 12f, 0f, 2.5f);
            }

            _world.SpawnBuilding(1, float3.zero, new float2(10f, 2f), complete: false);

            _world.Run(0.5f);

            Assert.IsFalse(Manager.Exists(overhanging), "a trunk 2 m off the edge hangs its canopy over the site");
            Assert.IsTrue(Manager.Exists(clear), "a canopy that stops short stands");
        }

        [Test]
        public void SolidScenery_StaysAndStillBlocksPlacement()
        {
            var wall = SpawnScenery(new float3(6f, 0f, 0f), block: new float2(4f, 4f));
            _world.SpawnBuilding(1, float3.zero, new float2(10f, 2f), complete: false);

            _world.Run(0.5f);

            Assert.IsTrue(Manager.Exists(wall), "solid scenery over the site's edge is an obstacle, not cleared");
            var spot = new BuildingSpot(new float3(2f, 0f, 20f), 0f, new float2(4f, 4f), PlacementSurface.Land);
            var blocked = new BuildingSpot(new float3(8f, 0f, 0f), 0f, new float2(2f, 2f), PlacementSurface.Land);
            var map = _world.Singleton<MapSettings>();
            var grid = _world.Singleton<NavGrid>();
            Assert.IsTrue(spot.IsValid(in map, in grid), "open ground takes a building");
            Assert.IsFalse(blocked.IsValid(in map, in grid), "the wall's cells refuse one");
        }
    }
}
