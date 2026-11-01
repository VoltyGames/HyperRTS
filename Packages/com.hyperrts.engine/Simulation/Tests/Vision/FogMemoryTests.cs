using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Vision
{
    /// <summary>Last-seen buildings: remembered when they leave sight, forgotten when seen again or found gone.</summary>
    public class FogMemoryTests : SimulationFixture
    {
        private static readonly float3 BuildingAt = new(5f, 0f, 0f);
        private static readonly float3 FarAway = new(60f, 0f, 60f);

        private Entity _scout;

        [SetUp]
        public void Prepare()
        {
            _scout = World.SpawnUnit(1, float3.zero);
        }

        [Test]
        public void Memory_CreatedWhenBuildingLeavesSight_AndKeptInFog()
        {
            var building = SightedBuilding(2);

            MoveScout(FarAway);
            var memory = SingleMemory();
            World.Run(3f);

            Assert.AreEqual(World.Get<EntityInfo>(building).TypeId, memory.State.TypeId);
            Assert.AreEqual(2, memory.State.Faction);
            Assert.AreEqual(BuildingAt, memory.State.Transform.Position);
            Assert.AreEqual(1, CountMemories(World), "the memory stays while its place is in the fog");
        }

        [Test]
        public void Memory_RemovedWhenBuildingIsSeenAlive()
        {
            var building = SightedBuilding(2);
            MoveScout(FarAway);
            SingleMemory();

            MoveScout(float3.zero);

            Assert.AreEqual(0, CountMemories(World));
            Assert.IsTrue(World.EntityManager.HasComponent<LatestSeen>(building), "seen again, it is sighted anew");
        }

        [Test]
        public void Memory_RemovedOnlyAfterGrace_WhenItsPlaceIsFoundEmpty()
        {
            var building = SightedBuilding(2);
            MoveScout(FarAway);
            SingleMemory();
            World.EntityManager.DestroyEntity(building);

            MoveScout(float3.zero);
            Assert.AreEqual(1, CountMemories(World), "kept a moment, as a client gets the live building later");
            World.Run(FogMemorySystem.Grace);

            Assert.AreEqual(0, CountMemories(World));
        }

        [Test]
        public void BuildingVanishingOutOfSight_IsRemembered()
        {
            var building = SightedBuilding(2);

            // A client's ghost is despawned once the server stops sending it, which a destroyed entity stands in for.
            MoveScoutAndBlind(FarAway);
            World.EntityManager.DestroyEntity(building);
            World.Tick();

            Assert.AreEqual(1, CountMemories(World));
            Assert.IsFalse(World.EntityManager.Exists(building), "the latest released the despawned root");
        }

        [Test]
        public void BuildingDestroyedInView_IsNotRemembered()
        {
            var building = SightedBuilding(2);

            World.EntityManager.DestroyEntity(building);
            World.Tick();

            Assert.AreEqual(0, CountMemories(World));
            Assert.IsFalse(World.EntityManager.Exists(building));
        }

        [Test]
        public void LiveChanges_DoNotLeakIntoTheMemory()
        {
            var building = SightedBuilding(2);
            MoveScout(FarAway);
            SingleMemory();

            var health = World.Get<Health>(building);
            health.Current = health.Max * 0.25f;
            World.Set(building, health);
            World.Run(0.5f);

            Assert.AreEqual(1f, SingleMemory().State.Health);
        }

        [Test]
        public void AlliedBuildings_AreNeverRemembered()
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 1);
            var scout = world.SpawnUnit(1, float3.zero);
            world.SpawnBuilding(2, BuildingAt, new float2(4f, 4f));
            world.Run(0.5f);

            world.Set(scout, LocalTransform.FromPosition(FarAway));
            world.Run(0.5f);

            Assert.AreEqual(0, CountMemories(world));
        }

        [Test]
        public void NoMemories_WithFogOff()
        {
            World.DisableFog();
            var building = SightedBuilding(2);

            MoveScout(FarAway);

            Assert.AreEqual(0, CountMemories(World));
            Assert.IsFalse(World.EntityManager.HasComponent<LatestSeen>(building));
        }

        [Test]
        public void RevealAll_ForgetsEveryMemory()
        {
            SightedBuilding(2);
            MoveScout(FarAway);
            SingleMemory();

            LocalFogViewSystem.SetRevealAll(World.EntityManager, true);

            Assert.AreEqual(0, CountMemories(World));
        }

        private Entity SightedBuilding(byte faction)
        {
            var building = World.SpawnBuilding(faction, BuildingAt, new float2(4f, 4f));
            World.Run(0.5f);
            return building;
        }

        private void MoveScout(float3 position)
        {
            World.Set(_scout, LocalTransform.FromPosition(position));
            World.Run(0.5f);
        }

        /// <summary>Moves the scout and clears its team's sight at once instead of at the next restamp.</summary>
        private void MoveScoutAndBlind(float3 position)
        {
            World.Set(_scout, LocalTransform.FromPosition(position));
            var visible = World.Fog().Visible;
            for (var i = 0; i < visible.Length; i++)
            {
                visible[i] = 0;
            }
        }

        private FogMemory SingleMemory()
        {
            using var query = World.Query(typeof(FogMemory));
            Assert.AreEqual(1, query.CalculateEntityCount());
            return query.GetSingleton<FogMemory>();
        }

        private static int CountMemories(TestWorld world)
        {
            using var query = world.Query(typeof(FogMemory));
            return query.CalculateEntityCount();
        }
    }
}
