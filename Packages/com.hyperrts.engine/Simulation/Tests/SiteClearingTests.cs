using HyperRTS.Network.Scenery;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// A building site removes the open scenery its footprint overlaps and the remains on it, quietly, and
    /// multiplayer tells clients so with the Cleared flag.
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
            var writer = new EntityManagerWriter(Manager, entity);
            DestructibleSetup.AddScenery(ref writer, 50f, 1, remains, block, SceneryId.At(position, quaternion.identity));
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
        public void TreeBesideTheSite_GoesWhenItsCrownReachesOver()
        {
            var overhanging = SpawnScenery(new float3(0f, 0f, 3f));
            var clear = SpawnScenery(new float3(0f, 0f, -5f));
            foreach (var tree in new[] { overhanging, clear })
            {
                var writer = new EntityManagerWriter(Manager, tree);
                DestructibleSetup.AddTopple(ref writer, new float3(0f, 1f, 0f), 12f, 0f, 2.5f);
            }

            _world.SpawnBuilding(1, float3.zero, new float2(10f, 2f), complete: false);

            _world.Run(0.5f);

            Assert.IsFalse(Manager.Exists(overhanging), "a trunk 2 m off the edge hangs its crown over the site");
            Assert.IsTrue(Manager.Exists(clear), "a crown that stops short stands");
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

        [Test]
        public void SceneryChange_CarriesClearedThroughItsCode()
        {
            var change = new SceneryChange { Id = 7, State = SceneryChange.Destroyed, Fall = 3, Cleared = true };

            var back = SceneryChange.FromCode(change.Id, change.Code, change.Fall);

            Assert.IsTrue(back.Cleared);
            Assert.AreEqual(SceneryChange.Destroyed, back.State);
            Assert.AreEqual(3, back.Fall);
            Assert.IsFalse(SceneryChange.FromCode(7, new SceneryChange { State = 120 }.Code, 0).Cleared);
        }

        [Test]
        public void Tracker_TellsTeamsOfAClearing_EvenAfterTheyHeardOfTheDestruction()
        {
            using var tracker = new SceneryTracker(Allocator.Temp);
            var sightings = new NativeList<ScenerySighting>(Allocator.Temp);
            tracker.Register(9, float3.zero);
            tracker.Set(9, SceneryChange.Destroyed, 0, 0b10, 1);
            tracker.Reveal(default(FogOfWar), false, 1, sightings);
            sightings.Clear();

            Assert.IsTrue(tracker.Clear(9, 0b10, 2));
            Assert.IsFalse(tracker.Clear(9, 0b10, 3), "a piece is cleared once");
            tracker.Reveal(default(FogOfWar), false, 2, sightings);
            var known = new NativeList<SceneryChange>(Allocator.Temp);
            tracker.Known(1, known);

            Assert.AreEqual(1, sightings.Length);
            Assert.IsTrue(sightings[0].Change.Cleared, "the team hears its remains were cleared");
            Assert.IsTrue(known[0].Cleared, "a joining client starts from the clearing");
        }
    }
}
