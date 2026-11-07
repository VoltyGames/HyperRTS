using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Production
{
    /// <summary>Why a purchase is refused, in the order the HUD shows: a lock before the price.</summary>
    public class PurchaseRefusalTests : SimulationFixture
    {
        private static readonly float3 Far = new(60f, 0f, 60f);

        private ResourceType _supplies;

        [SetUp]
        public void Prepare() => _supplies = World.CreateSupplies();

        [Test]
        public void FindRefusal_NamesAMissingPrerequisite_BeforeThePrice()
        {
            var prefab = World.UnitPrefab(_supplies, 100);
            World.EntityManager.GetBuffer<Prerequisite>(prefab)
                .Add(new Prerequisite { TypeId = EntityInfo.TypeIdFromName("Barracks") });

            Assert.AreEqual(PurchaseRefusal.MissingPrerequisite, Refusal(prefab), "locked and unaffordable");

            World.SpawnBuilding(1, Far, new float2(4f, 4f), name: "Barracks");
            Assert.AreEqual(PurchaseRefusal.CannotAfford, Refusal(prefab));

            World.AddResources(1, _supplies, 100);
            Assert.AreEqual(PurchaseRefusal.None, Refusal(prefab));
        }

        [Test]
        public void FindRefusal_NamesAReachedLimit_AndFinishedResearch()
        {
            var entityManager = World.EntityManager;
            World.AddResources(1, _supplies, 1000);
            var hero = World.AddBuildLimit(World.UnitPrefab(_supplies, 10, name: "Hero"), 1);
            World.AddBuildLimit(World.SpawnUnit(1, Far, name: "Hero"), 1);
            var upgrade = World.UnitPrefab(_supplies, 10, name: "Upgrade");
            entityManager.AddComponent<Research>(upgrade);
            entityManager.GetBuffer<CompletedResearch>(World.Player(1)).Add(new CompletedResearch
            {
                Prefab = upgrade, TypeId = EntityInfo.TypeIdFromName("Upgrade"),
            });

            Assert.AreEqual(PurchaseRefusal.AtLimit, Refusal(hero));
            Assert.AreEqual(PurchaseRefusal.Researched, Refusal(upgrade));
        }

        private PurchaseRefusal Refusal(Entity prefab)
        {
            var entityManager = World.EntityManager;
            using var queues = ResearchRules.QueueQuery(Allocator.Temp).Build(entityManager);
            using var limited = BuildCounts.Query(Allocator.Temp).Build(entityManager);
            using var completed = CompletedBuildings.Query(Allocator.Temp).Build(entityManager);
            var player = World.Player(1);
            var buyer = Buyer.Snapshot(entityManager, queues, limited, 1,
                new CompletedBuildings(completed, Allocator.Temp), entityManager.GetBuffer<CompletedResearch>(player));
            var stock = entityManager.GetBuffer<ResourceStock>(player);
            return PurchaseRules.FindRefusal(entityManager, prefab, buyer, stock.AsNativeArray());
        }
    }
}
