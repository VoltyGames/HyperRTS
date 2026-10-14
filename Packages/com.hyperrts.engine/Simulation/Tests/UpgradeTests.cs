using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Upgrades;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Research through the production queue and upgrade bonuses on current and future units.</summary>
    public class UpgradeTests
    {
        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = ScriptableObject.CreateInstance<ResourceType>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_supplies);
        }

        private Entity UpgradePrefab(string name, int appliesTo)
        {
            var upgrade = _world.EntityManager.CreateEntity();
            var writer = new EntityManagerWriter(_world.EntityManager, upgrade);
            var typeId = EntityInfo.TypeIdFromName(name);
            UpgradeSetup.Add(ref writer, new EntityInfo { TypeId = typeId, Name = name }, 2f,
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
            return _world.MakePrefab(upgrade);
        }

        [Test]
        public void Research_AppliesToMatchingUnits_IncludingLaterSpawns()
        {
            var tankType = EntityInfo.TypeIdFromName("Tank");
            var upgrade = UpgradePrefab("Armor Plating", tankType);
            var lab = _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), float3.zero, upgrade);
            var tank = _world.SpawnUnit(1, new float3(10f, 0f, 0f), name: "Tank");
            var jeep = _world.SpawnUnit(1, new float3(12f, 0f, 0f), name: "Jeep");
            var enemyTank = _world.SpawnUnit(2, new float3(-30f, 0f, 0f), name: "Tank");
            _world.AddResources(1, _supplies, 100);

            _world.Produce(1, lab, upgrade);
            _world.Run(3f);

            Assert.AreEqual(50, _world.GetResources(1, _supplies));
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<CompletedResearch>(_world.Player(1)).Length);
            Assert.AreEqual(150f, _world.Get<Health>(tank).Max);
            Assert.AreEqual(100f, _world.Get<Health>(jeep).Max, "other types are unaffected");
            Assert.AreEqual(100f, _world.Get<Health>(enemyTank).Max, "other players are unaffected");

            var newTank = _world.SpawnUnit(1, new float3(14f, 0f, 0f), name: "Tank");
            _world.Tick(frames: 2);
            Assert.AreEqual(150f, _world.Get<Health>(newTank).Max, "later spawns catch up");
        }

        [Test]
        public void Upgrade_CanOnlyBeResearchedOnce()
        {
            var upgrade = UpgradePrefab("Optics", 0);
            var lab = _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), float3.zero, upgrade);
            _world.AddResources(1, _supplies, 500);

            _world.Produce(1, lab, upgrade);
            _world.Produce(1, lab, upgrade);
            _world.Tick();
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<ProductionQueueItem>(lab).Length, "no duplicate queue");

            _world.Run(3f);
            _world.Produce(1, lab, upgrade);
            _world.Tick();

            Assert.AreEqual(0, _world.EntityManager.GetBuffer<ProductionQueueItem>(lab).Length, "already researched");
            Assert.AreEqual(450, _world.GetResources(1, _supplies));
        }

        [Test]
        public void FinishedResearch_SatisfiesAPrerequisite()
        {
            var doctrine = UpgradePrefab("Doctrine", 0);
            var tank = _world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 90f), name: "Tank"));
            var required = new Prerequisite { TypeId = EntityInfo.TypeIdFromName("Doctrine") };
            _world.EntityManager.GetBuffer<Prerequisite>(tank).Add(required);
            var lab = _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), float3.zero,
                doctrine, tank);
            _world.AddResources(1, _supplies, 100);

            _world.Produce(1, lab, tank);
            _world.Tick();
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<ProductionQueueItem>(lab).Length, "not researched yet");

            _world.Produce(1, lab, doctrine);
            _world.Run(3f);
            _world.Produce(1, lab, tank);
            _world.Tick();

            Assert.AreEqual(1, _world.EntityManager.GetBuffer<ProductionQueueItem>(lab).Length);
        }

        [TestCase(false, TestName = "CapturedEntity_SwapsToNewOwnersUpgrades")]
        [TestCase(true, TestName = "CapturedEntity_LosesUpgradesWhenTheOldOwnerIsGone")]
        public void CapturedEntity_DropsTheOldOwnersUpgrades(bool oldOwnerGone)
        {
            var upgrade = UpgradePrefab("Sensors", 0);
            var lab = _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), float3.zero, upgrade);
            var unit = _world.SpawnUnit(1, new float3(10f, 0f, 0f));
            _world.AddResources(1, _supplies, 100);
            _world.Produce(1, lab, upgrade);
            _world.Run(3f);
            Assert.AreEqual(150f, _world.Get<Health>(unit).Max);

            if (oldOwnerGone)
            {
                _world.EntityManager.DestroyEntity(_world.Player(1));
            }

            _world.EntityManager.SetComponentData(unit, new Faction { Value = 2 });
            _world.Tick(frames: 2);

            Assert.AreEqual(100f, _world.Get<Health>(unit).Max);
        }
    }
}
