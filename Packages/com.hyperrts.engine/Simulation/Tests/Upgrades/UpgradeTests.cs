using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Upgrades;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Upgrades
{
    /// <summary>Research through the production queue and upgrade bonuses on current and future units.</summary>
    public class UpgradeTests : SimulationFixture
    {
        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
        }

        private Entity UpgradePrefab(string name, int appliesTo)
        {
            var upgrade = World.EntityManager.CreateEntity();
            var writer = World.Writer(upgrade);
            var typeId = EntityInfo.TypeIdFromName(name);
            UpgradeAuthoring.Add(writer, new EntityInfo { TypeId = typeId, Name = name }, 2f,
                new[] { new ResourceCost { Type = _supplies, Amount = 50 } }, new Prerequisite[0], new[]
                {
                    new UpgradeEffect
                    {
                        AppliesTo = appliesTo,
                        Modifier = new StatModifier
                        {
                            Stat = Stat.MaxHealth, Add = 50f, Source = StatSource.Upgrade(typeId),
                        },
                    },
                });
            return World.MakePrefab(upgrade);
        }

        [Test]
        public void Research_AppliesToMatchingUnits_IncludingLaterSpawns()
        {
            var tankType = EntityInfo.TypeIdFromName("Tank");
            var upgrade = UpgradePrefab("Armor Plating", tankType);
            var lab = World.MakeProducer(World.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), float3.zero, upgrade);
            var tank = World.SpawnUnit(1, new float3(10f, 0f, 0f), new TestUnit { Name = "Tank" });
            var jeep = World.SpawnUnit(1, new float3(12f, 0f, 0f), new TestUnit { Name = "Jeep" });
            var enemyTank = World.SpawnUnit(2, new float3(-30f, 0f, 0f), new TestUnit { Name = "Tank" });
            World.AddResources(1, _supplies, 100);

            World.Produce(1, lab, upgrade);
            World.Run(3f);

            Assert.AreEqual(50, World.GetResources(1, _supplies));
            Assert.AreEqual(1, World.EntityManager.GetBuffer<CompletedResearch>(World.Player(1)).Length);
            Assert.AreEqual(150f, World.Get<Health>(tank).Max);
            Assert.AreEqual(100f, World.Get<Health>(jeep).Max, "other types are unaffected");
            Assert.AreEqual(100f, World.Get<Health>(enemyTank).Max, "other players are unaffected");

            var newTank = World.SpawnUnit(1, new float3(14f, 0f, 0f), new TestUnit { Name = "Tank" });
            World.Tick(frames: 2);
            Assert.AreEqual(150f, World.Get<Health>(newTank).Max, "later spawns catch up");
        }

        [Test]
        public void Upgrade_CanOnlyBeResearchedOnce()
        {
            var upgrade = UpgradePrefab("Optics", 0);
            var lab = World.MakeProducer(World.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), float3.zero, upgrade);
            World.AddResources(1, _supplies, 500);

            World.Produce(1, lab, upgrade);
            World.Produce(1, lab, upgrade);
            World.Tick();
            Assert.AreEqual(1, World.Buffer<ProductionQueueItem>(lab).Length, "no duplicate queue");

            World.Run(3f);
            World.Produce(1, lab, upgrade);
            World.Tick();

            Assert.AreEqual(0, World.Buffer<ProductionQueueItem>(lab).Length, "already researched");
            Assert.AreEqual(450, World.GetResources(1, _supplies));
        }

        [Test]
        public void FinishedResearch_SatisfiesAPrerequisite()
        {
            var doctrine = UpgradePrefab("Doctrine", 0);
            var tank = World.MakePrefab(World.SpawnUnit(0, new float3(90f, 0f, 90f), new TestUnit { Name = "Tank" }));
            var required = new Prerequisite { TypeId = EntityInfo.TypeIdFromName("Doctrine") };
            World.Buffer<Prerequisite>(tank).Add(required);
            var lab = World.MakeProducer(World.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), float3.zero,
                doctrine, tank);
            World.AddResources(1, _supplies, 100);

            World.Produce(1, lab, tank);
            World.Tick();
            Assert.AreEqual(0, World.Buffer<ProductionQueueItem>(lab).Length, "not researched yet");

            World.Produce(1, lab, doctrine);
            World.Run(3f);
            World.Produce(1, lab, tank);
            World.Tick();

            Assert.AreEqual(1, World.Buffer<ProductionQueueItem>(lab).Length);
        }

        [TestCase(false, TestName = "CapturedEntity_SwapsToNewOwnersUpgrades")]
        [TestCase(true, TestName = "CapturedEntity_LosesUpgradesWhenTheOldOwnerIsGone")]
        public void CapturedEntity_DropsTheOldOwnersUpgrades(bool oldOwnerGone)
        {
            var upgrade = UpgradePrefab("Sensors", 0);
            var lab = World.MakeProducer(World.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), float3.zero, upgrade);
            var unit = World.SpawnUnit(1, new float3(10f, 0f, 0f));
            World.AddResources(1, _supplies, 100);
            World.Produce(1, lab, upgrade);
            World.Run(3f);
            Assert.AreEqual(150f, World.Get<Health>(unit).Max);

            if (oldOwnerGone)
            {
                World.EntityManager.DestroyEntity(World.Player(1));
            }

            World.Set(unit, new Owner { Value = 2 });
            World.Tick(frames: 2);

            Assert.AreEqual(100f, World.Get<Health>(unit).Max);
        }
    }
}
