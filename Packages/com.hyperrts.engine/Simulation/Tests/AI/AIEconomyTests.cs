using System.Collections.Generic;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.AI
{
    /// <summary>Skirmish AI economy: harvesting in the map's expansion order, spending across producers, build orders.</summary>
    public class AIEconomyTests
    {
        private const float BuildingGap = 4f;

        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = _world.CreateSupplies();
            var writer = new EntityManagerWriter(_world.EntityManager, _world.Player(2));
            AIPlayerSetup.Add(ref writer,
                new AIPlayer { ThinkInterval = 0.5f, AttackWaveSize = 50, BuildingGap = BuildingGap });
            var map = _world.Singleton<MapSettings>();
            map.BuildingGap = BuildingGap;
            _world.EntityManager.SetComponentData(_world.MatchEntity(), map);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void AI_SendsIdleHarvesterToTheFirstFieldOfItsExpansionOrder_NotTheNearest()
        {
            var harvester = _world.MakeHarvester(_world.SpawnUnit(2, float3.zero), 10, 1f);
            var main = _world.SpawnNode(new float3(30f, 0f, 0f), _supplies, 100);
            _world.SpawnNode(new float3(5f, 0f, 0f), _supplies, 100);
            ExpandInOrder(new Vector2(30f, 0f), new Vector2(5f, 0f));

            _world.Tick();

            Assert.IsTrue(_world.IsEnabled<ActiveOrder>(harvester));
            Assert.AreEqual(OrderType.Gather, _world.Get<ActiveOrder>(harvester).Value.Type);
            Assert.AreEqual(main, _world.Get<ActiveOrder>(harvester).Value.Target);
        }

        [Test]
        public void AI_MovesToTheNextFieldInOrder_OnceTheFirstIsSpent()
        {
            var harvester = _world.MakeHarvester(_world.SpawnUnit(2, float3.zero), 10, 1f);
            var spent = _world.SpawnNode(new float3(30f, 0f, 0f), _supplies, 100);
            var next = _world.SpawnNode(new float3(60f, 0f, 0f), _supplies, 100);
            _world.SpawnNode(new float3(5f, 0f, 0f), _supplies, 100);
            ExpandInOrder(new Vector2(30f, 0f), new Vector2(60f, 0f), new Vector2(5f, 0f));
            var node = _world.Get<ResourceNode>(spent);
            node.Amount = 0;
            _world.EntityManager.SetComponentData(spent, node);

            _world.Tick();

            Assert.AreEqual(next, _world.Get<ActiveOrder>(harvester).Value.Target);
        }

        [Test]
        public void AI_IgnoresFieldsTheMapDoesNotHint()
        {
            var harvester = _world.MakeHarvester(_world.SpawnUnit(2, float3.zero), 10, 1f);
            _world.SpawnNode(new float3(5f, 0f, 0f), _supplies, 100);
            ExpandInOrder(new Vector2(30f, 0f));

            _world.Tick();

            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(harvester), "no hinted field has resources");
        }

        [Test]
        public void AI_BudgetsItsStockpile_AcrossIdleProducers()
        {
            var soldier = UnitPrefab("Soldier", 10, 30f);
            var writer = new EntityManagerWriter(_world.EntityManager, soldier);
            WeaponSetup.Add(ref writer, new Weapon { Range = 2f, Damage = 1f, Cooldown = 1f, LeashFactor = 1.5f },
                Stance.Aggressive, float3.zero, 0);
            var tank = UnitPrefab("Tank", 15, 30f);
            var first = _world.MakeProducer(_world.SpawnBuilding(2, float3.zero, new float2(4f, 4f)),
                new float3(0f, 0f, -4f), soldier, tank);
            var second = _world.MakeProducer(_world.SpawnBuilding(2, new float3(10f, 0f, 0f), new float2(4f, 4f)),
                new float3(0f, 0f, -4f), soldier, tank);
            _world.SpawnProvider(2, new float3(-20f, 0f, 0f), 10);
            _world.AddResources(2, _supplies, 20);

            _world.Tick(frames: 3);

            // With the soldier's cost set aside, the tank is out of reach and the second producer trains a soldier too.
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<ProductionQueueItem>(first).Length);
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<ProductionQueueItem>(second).Length);
            Assert.AreEqual(0, _world.GetResources(2, _supplies));
        }

        [Test]
        public void AI_QueuesAResearchOnce_AcrossIdleProducers()
        {
            var research = _world.EntityManager.CreateEntity();
            var writer = new EntityManagerWriter(_world.EntityManager, research);
            UpgradeAuthoring.Add(ref writer, new EntityInfo { TypeId = EntityInfo.TypeIdFromName("Optics"), Name = "Optics" },
                30f, new[] { new ResourceCost { Type = _supplies, Amount = 50 } }, new Prerequisite[0],
                new UpgradeEffect[0]);
            research = _world.MakePrefab(research);
            var first = _world.MakeProducer(_world.SpawnBuilding(2, float3.zero, new float2(4f, 4f)), float3.zero,
                research);
            var second = _world.MakeProducer(_world.SpawnBuilding(2, new float3(10f, 0f, 0f), new float2(4f, 4f)),
                float3.zero, research);
            _world.AddResources(2, _supplies, 500);

            _world.Tick(frames: 3);

            var queued = _world.EntityManager.GetBuffer<ProductionQueueItem>(first).Length +
                         _world.EntityManager.GetBuffer<ProductionQueueItem>(second).Length;
            Assert.AreEqual(1, queued);
            Assert.AreEqual(450, _world.GetResources(2, _supplies));
        }

        [Test]
        public void BuildOrder_PlacesBuildingWithBuilder_ThenTrainsUnit()
        {
            var (barracks, soldier) = BuildOrderBase();
            SpawnBuilder(new float3(0f, 0f, -6f), barracks);
            SetBuildOrder((barracks, 1), (soldier, 1));
            _world.AddResources(2, _supplies, 100);

            _world.Run(6f);

            var placed = Owned("Barracks");
            Assert.AreEqual(1, placed.Count, "one barracks placed");
            Assert.IsFalse(_world.IsEnabled<ConstructionProgress>(placed[0]), "the builder finished it");
            var offset = math.abs(_world.Get<LocalTransform>(placed[0]).Position.xz);
            Assert.GreaterOrEqual(math.cmax(offset) - 4f, BuildingGap - 1e-3f, "a lane is left next to the HQ");
            Assert.GreaterOrEqual(Owned("Soldier").Count, 1, "then a soldier was trained there");
        }

        [Test]
        public void BuildOrder_KeepsLanesOfTheBuildingGapBetweenTheBasesBuildings()
        {
            var (barracks, _) = BuildOrderBase();
            SpawnBuilder(new float3(0f, 0f, -6f), barracks);
            SetBuildOrder((barracks, 3));
            _world.AddResources(2, _supplies, 1000);

            _world.Run(12f);

            var buildings = Owned("Barracks");
            buildings.AddRange(Owned("HQ"));
            Assert.AreEqual(4, buildings.Count, "three barracks beside the HQ");
            for (var i = 0; i < buildings.Count; i++)
            {
                for (var j = i + 1; j < buildings.Count; j++)
                {
                    var gap = Box(buildings[i]).Gap(Box(buildings[j]));
                    Assert.GreaterOrEqual(gap, BuildingGap - 1e-3f, $"a lane between buildings {i} and {j}");
                }
            }
        }

        private NavBox Box(Entity building)
        {
            var transform = _world.Get<LocalTransform>(building);
            return new NavBox(transform.Position, transform.Rotation, _world.Get<NavObstacle>(building).Size);
        }

        [Test]
        public void BuildOrder_CountsSitesAndQueuedUnits_AndNeverOverbuilds()
        {
            var (barracks, soldier) = BuildOrderBase();
            SpawnBuilder(new float3(0f, 0f, -6f), barracks);
            SpawnBuilder(new float3(6f, 0f, -6f), barracks);
            var locked = _world.MakePrefab(_world.SpawnBuilding(0, float3.zero, new float2(2f, 2f), name: "Locked"));
            _world.EntityManager.GetBuffer<Prerequisite>(locked)
                .Add(new Prerequisite { TypeId = EntityInfo.TypeIdFromName("Never Built") });
            SetBuildOrder((barracks, 2), (soldier, 3), (locked, 1));
            _world.AddResources(2, _supplies, 1000);

            _world.Run(12f);

            Assert.AreEqual(2, Owned("Barracks").Count);
            Assert.AreEqual(3, Owned("Soldier").Count, "the unmet locked step keeps the AI from training freely");
            Assert.AreEqual(1000 - 2 * 50 - 3 * 10, _world.GetResources(2, _supplies));
        }

        [Test]
        public void BuildOrder_SkipsUnaffordableStep_UntilItCanPay()
        {
            var (barracks, soldier) = BuildOrderBase();
            SpawnBuilder(new float3(0f, 0f, -6f), barracks);
            ExistingBarracks(soldier);
            SetBuildOrder((barracks, 2), (soldier, 1));
            _world.AddResources(2, _supplies, 20);

            _world.Run(2f);

            Assert.AreEqual(1, Owned("Barracks").Count, "too poor for the second barracks");
            Assert.AreEqual(1, Owned("Soldier").Count, "so it trained the affordable next step");
        }

        [Test]
        public void BuildOrder_SkipsStepAtItsBuildLimit_AndTrainsFreely()
        {
            var (barracks, soldier) = BuildOrderBase();
            SpawnBuilder(new float3(0f, 0f, -6f), barracks);
            var existing = ExistingBarracks(soldier);
            _world.AddBuildLimit(barracks, 1);
            _world.AddBuildLimit(existing, 1);
            SetBuildOrder((barracks, 2), (soldier, 1));
            _world.AddResources(2, _supplies, 1000);

            _world.Run(6f);

            Assert.AreEqual(1, Owned("Barracks").Count, "the limit holds");
            Assert.Greater(Owned("Soldier").Count, 1, "the capped step counts as met, so the AI trains past the order");
        }

        /// <summary>Slot 2's expansion order is the given positions (x, z); the world's other hints stay.</summary>
        private void ExpandInOrder(params Vector2[] fields)
        {
            var data = new AIMapHintsData();
            data.AddSlot(1, new Vector2[0], new Vector2[0]);
            data.AddSlot(2, fields, new Vector2[0]);
            data.AddLane(1, 2, new Vector2[0]);
            data.AddLane(2, 1, new Vector2[0]);
            _world.CreateHints(data);
        }

        private Entity UnitPrefab(string name, int cost, float buildTime)
        {
            var prefab = _world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 90f), name: name));
            _world.SetBuildTime(prefab, buildTime);
            _world.SetCost(prefab, _supplies, cost);
            return prefab;
        }

        /// <summary>Slot 2's HQ, and a barracks prefab that trains the soldier prefab.</summary>
        private (Entity Barracks, Entity Soldier) BuildOrderBase()
        {
            _world.SpawnBuilding(2, float3.zero, new float2(4f, 4f), name: "HQ", populationProvided: 20);
            var soldier = UnitPrefab("Soldier", 10, 0.5f);
            var barracks = _world.MakePrefab(_world.SpawnBuilding(0, float3.zero, new float2(4f, 4f), complete: false,
                buildTime: 1f, name: "Barracks"));
            _world.SetCost(barracks, _supplies, 50);
            _world.MakeProducer(barracks, new float3(0f, 0f, -3f), soldier);
            return (barracks, soldier);
        }

        private Entity ExistingBarracks(Entity soldier)
        {
            var existing = _world.SpawnBuilding(2, new float3(20f, 0f, 20f), new float2(4f, 4f), name: "Barracks");
            return _world.MakeProducer(existing, new float3(0f, 0f, -3f), soldier);
        }

        private void SpawnBuilder(float3 position, Entity barracks) =>
            _world.MakeBuilder(_world.SpawnUnit(2, position, name: "Builder"), barracks);

        private void SetBuildOrder(params (Entity Prefab, int Count)[] steps)
        {
            var buffer = _world.EntityManager.GetBuffer<AIBuildStep>(_world.Player(2));
            foreach (var step in steps)
            {
                buffer.Add(new AIBuildStep { Prefab = step.Prefab, Count = step.Count });
            }
        }

        private List<Entity> Owned(string name) => _world.Owned(EntityInfo.TypeIdFromName(name), 2);
    }
}
