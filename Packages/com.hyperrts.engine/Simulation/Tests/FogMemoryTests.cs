using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Last-seen buildings: remembered when they leave sight, forgotten when seen again or found gone.</summary>
    public class FogMemoryTests
    {
        private static readonly float3 BuildingAt = new(5f, 0f, 0f);
        private static readonly float3 FarAway = new(60f, 0f, 60f);

        private TestWorld _world;
        private Entity _scout;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _scout = _world.SpawnUnit(1, float3.zero);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Memory_CreatedWhenBuildingLeavesSight_AndKeptInFog()
        {
            var building = SightedBuilding(2);

            MoveScout(FarAway);
            var memory = SingleMemory();
            _world.Run(3f);

            Assert.AreEqual(_world.Get<EntityInfo>(building).TypeId, memory.Record.TypeId);
            Assert.AreEqual(2, memory.Record.Faction);
            Assert.AreEqual(BuildingAt, memory.Record.Transform.Position);
            Assert.AreEqual(1, CountMemories(_world), "the memory stays while its place is in the fog");
        }

        [Test]
        public void Memory_RemovedWhenBuildingIsSeenAlive()
        {
            var building = SightedBuilding(2);
            MoveScout(FarAway);
            SingleMemory();

            MoveScout(float3.zero);

            Assert.AreEqual(0, CountMemories(_world));
            Assert.IsTrue(_world.EntityManager.HasComponent<FogSighting>(building), "seen again, it is sighted anew");
        }

        [Test]
        public void Memory_RemovedOnlyAfterGrace_WhenItsPlaceIsFoundEmpty()
        {
            var building = SightedBuilding(2);
            MoveScout(FarAway);
            SingleMemory();
            _world.EntityManager.DestroyEntity(building);

            MoveScout(float3.zero);
            Assert.AreEqual(1, CountMemories(_world), "kept a moment, as a client gets the live building later");
            _world.Run(FogMemorySystem.Grace);

            Assert.AreEqual(0, CountMemories(_world));
        }

        [Test]
        public void BuildingVanishingOutOfSight_IsRemembered()
        {
            var building = SightedBuilding(2);

            // A client's ghost is despawned once the server stops sending it, which a destroyed entity stands in for.
            MoveScoutAndBlind(FarAway);
            _world.EntityManager.DestroyEntity(building);
            _world.Tick();

            Assert.AreEqual(1, CountMemories(_world));
            Assert.IsFalse(_world.EntityManager.Exists(building), "the sighting released the despawned root");
        }

        [Test]
        public void BuildingDestroyedInView_IsNotRemembered()
        {
            var building = SightedBuilding(2);

            _world.EntityManager.DestroyEntity(building);
            _world.Tick();

            Assert.AreEqual(0, CountMemories(_world));
            Assert.IsFalse(_world.EntityManager.Exists(building));
        }

        [Test]
        public void LiveChanges_DoNotLeakIntoTheMemory()
        {
            var building = SightedBuilding(2);
            MoveScout(FarAway);
            SingleMemory();

            var health = _world.Get<Health>(building);
            health.Current = health.Max * 0.25f;
            _world.EntityManager.SetComponentData(building, health);
            _world.Run(0.5f);

            Assert.AreEqual(1f, SingleMemory().Record.Health);
        }

        [Test]
        public void AlliedBuildings_AreNeverRemembered()
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 1);
            var scout = world.SpawnUnit(1, float3.zero);
            world.SpawnBuilding(2, BuildingAt, new float2(4f, 4f));
            world.Run(0.5f);

            world.EntityManager.SetComponentData(scout, LocalTransform.FromPosition(FarAway));
            world.Run(0.5f);

            Assert.AreEqual(0, CountMemories(world));
        }

        [Test]
        public void NoMemories_WithFogOff()
        {
            _world.DisableFog();
            var building = SightedBuilding(2);

            MoveScout(FarAway);

            Assert.AreEqual(0, CountMemories(_world));
            Assert.IsFalse(_world.EntityManager.HasComponent<FogSighting>(building));
        }

        [Test]
        public void RevealAll_ForgetsEveryMemory()
        {
            SightedBuilding(2);
            MoveScout(FarAway);
            SingleMemory();

            LocalFogViewSystem.SetRevealAll(_world.EntityManager, true);

            Assert.AreEqual(0, CountMemories(_world));
        }

        private Entity SightedBuilding(byte faction)
        {
            var building = _world.SpawnBuilding(faction, BuildingAt, new float2(4f, 4f));
            _world.Run(0.5f);
            return building;
        }

        private void MoveScout(float3 position)
        {
            _world.EntityManager.SetComponentData(_scout, LocalTransform.FromPosition(position));
            _world.Run(0.5f);
        }

        /// <summary>Moves the scout and clears its team's sight at once instead of at the next restamp.</summary>
        private void MoveScoutAndBlind(float3 position)
        {
            _world.EntityManager.SetComponentData(_scout, LocalTransform.FromPosition(position));
            var visible = _world.Fog().Visible;
            for (var i = 0; i < visible.Length; i++)
            {
                visible[i] = 0;
            }
        }

        private LastSeen SingleMemory()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(LastSeen));
            Assert.AreEqual(1, query.CalculateEntityCount());
            return query.GetSingleton<LastSeen>();
        }

        private static int CountMemories(TestWorld world)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(LastSeen));
            return query.CalculateEntityCount();
        }
    }
}
