using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Resources.Gathering;
using HyperRTS.Simulation.Upgrades;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.AI
{
    /// <summary>Skirmish AI economy: harvesting in the map's expansion order, spending across producers, build orders.</summary>
    public class AIEconomyTests : SimulationFixture
    {
        private const float BuildingGap = 4f;

        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
            var writer = World.Writer(World.Player(2));
            AIPlayerSetup.Add(writer,
                new AIPlayer { ThinkInterval = 0.5f, AttackWaveSize = 50, BuildingGap = BuildingGap });
            var map = World.Singleton<MapSettings>();
            map.BuildingGap = BuildingGap;
            World.Set(World.MatchEntity(), map);
        }

        [Test]
        public void AI_SendsIdleHarvesterToTheFirstFieldOfItsExpansionOrder_NotTheNearest()
        {
            var harvester = World.MakeHarvester(World.SpawnUnit(2, float3.zero), 10, 1f);
            var main = World.SpawnNode(new float3(30f, 0f, 0f), _supplies, 100);
            World.SpawnNode(new float3(5f, 0f, 0f), _supplies, 100);
            ExpandInOrder(new Vector2(30f, 0f), new Vector2(5f, 0f));

            World.Tick();

            Assert.IsTrue(World.IsEnabled<ActiveOrder>(harvester));
            Assert.AreEqual(OrderType.Gather, World.Get<ActiveOrder>(harvester).Value.Type);
            Assert.AreEqual(main, World.Get<ActiveOrder>(harvester).Value.Target);
        }

        [Test]
        public void AI_MovesToTheNextFieldInOrder_OnceTheFirstIsSpent()
        {
            var harvester = World.MakeHarvester(World.SpawnUnit(2, float3.zero), 10, 1f);
            var spent = World.SpawnNode(new float3(30f, 0f, 0f), _supplies, 100);
            var next = World.SpawnNode(new float3(60f, 0f, 0f), _supplies, 100);
            World.SpawnNode(new float3(5f, 0f, 0f), _supplies, 100);
            ExpandInOrder(new Vector2(30f, 0f), new Vector2(60f, 0f), new Vector2(5f, 0f));
            var node = World.Get<ResourceNode>(spent);
            node.Amount = 0;
            World.Set(spent, node);

            World.Tick();

            Assert.AreEqual(next, World.Get<ActiveOrder>(harvester).Value.Target);
        }

        [Test]
        public void AI_IgnoresFieldsTheMapDoesNotHint()
        {
            var harvester = World.MakeHarvester(World.SpawnUnit(2, float3.zero), 10, 1f);
            World.SpawnNode(new float3(5f, 0f, 0f), _supplies, 100);
            ExpandInOrder(new Vector2(30f, 0f));

            World.Tick();

            Assert.IsFalse(World.IsEnabled<ActiveOrder>(harvester), "no hinted field has resources");
        }

        [Test]
        public void AI_BudgetsItsStockpile_AcrossIdleProducers()
        {
            var soldier = UnitPrefab("Soldier", 10, 30f);
            var writer = World.Writer(soldier);
            var weapon = new Weapon
            {
                Range = 2f, Damage = new DamageProfile { Amount = 1f }, Cooldown = 1f,
                Targets = WeaponTargets.Surface,
            };
            WeaponSetup.Add(writer, new[] { WeaponMount.Create(weapon, default) }, CombatStance.Create(Stance.Aggressive, float3.zero));
            var tank = UnitPrefab("Tank", 15, 30f);
            var first = World.MakeProducer(World.SpawnBuilding(2, float3.zero, new float2(4f, 4f)),
                new float3(0f, 0f, -4f), soldier, tank);
            var second = World.MakeProducer(World.SpawnBuilding(2, new float3(10f, 0f, 0f), new float2(4f, 4f)),
                new float3(0f, 0f, -4f), soldier, tank);
            World.SpawnProvider(2, new float3(-20f, 0f, 0f), 10);
            World.AddResources(2, _supplies, 20);

            World.Tick(frames: 3);

            // With the soldier's cost set aside, the tank is out of reach and the second producer trains a soldier too.
            Assert.AreEqual(1, World.Buffer<ProductionQueueItem>(first).Length);
            Assert.AreEqual(1, World.Buffer<ProductionQueueItem>(second).Length);
            Assert.AreEqual(0, World.GetResources(2, _supplies));
        }

        [Test]
        public void AI_QueuesAResearchOnce_AcrossIdleProducers()
        {
            var research = World.EntityManager.CreateEntity();
            var writer = World.Writer(research);
            UpgradeAuthoring.Add(writer, new EntityInfo { TypeId = EntityInfo.TypeIdFromName("Optics"), Name = "Optics" },
                30f, new[] { new ResourceCost { Type = _supplies, Amount = 50 } }, new Prerequisite[0],
                new UpgradeEffect[0]);
            research = World.MakePrefab(research);
            var first = World.MakeProducer(World.SpawnBuilding(2, float3.zero, new float2(4f, 4f)), float3.zero,
                research);
            var second = World.MakeProducer(World.SpawnBuilding(2, new float3(10f, 0f, 0f), new float2(4f, 4f)),
                float3.zero, research);
            World.AddResources(2, _supplies, 500);

            World.Tick(frames: 3);

            var queued = World.Buffer<ProductionQueueItem>(first).Length +
                         World.Buffer<ProductionQueueItem>(second).Length;
            Assert.AreEqual(1, queued);
            Assert.AreEqual(450, World.GetResources(2, _supplies));
        }

        [Test]
        public void BuildOrder_PlacesBuildingWithBuilder_ThenTrainsUnit()
        {
            var (barracks, soldier) = BuildOrderBase();
            SpawnBuilder(new float3(0f, 0f, -6f), barracks);
            SetBuildOrder((barracks, 1), (soldier, 1));
            World.AddResources(2, _supplies, 100);

            World.Run(6f);

            var placed = Owned("Barracks");
            Assert.AreEqual(1, placed.Count, "one barracks placed");
            Assert.IsFalse(World.IsEnabled<ConstructionProgress>(placed[0]), "the builder finished it");
            var offset = math.abs(World.Get<LocalTransform>(placed[0]).Position.xz);
            Assert.GreaterOrEqual(math.cmax(offset) - 4f, BuildingGap - 1e-3f, "a lane is left next to the HQ");
            Assert.GreaterOrEqual(Owned("Soldier").Count, 1, "then a soldier was trained there");
        }

        [Test]
        public void BuildOrder_KeepsLanesOfTheBuildingGapBetweenTheBasesBuildings()
        {
            var (barracks, _) = BuildOrderBase();
            SpawnBuilder(new float3(0f, 0f, -6f), barracks);
            SetBuildOrder((barracks, 3));
            World.AddResources(2, _supplies, 1000);

            World.Run(12f);

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
            var transform = World.Get<LocalTransform>(building);
            return new NavBox(transform.Position, transform.Rotation, World.Get<NavObstacle>(building).Size);
        }

        [Test]
        public void BuildOrder_CountsSitesAndQueuedUnits_AndNeverOverbuilds()
        {
            var (barracks, soldier) = BuildOrderBase();
            SpawnBuilder(new float3(0f, 0f, -6f), barracks);
            SpawnBuilder(new float3(6f, 0f, -6f), barracks);
            var locked = World.MakePrefab(World.SpawnBuilding(0, float3.zero, new float2(2f, 2f), new TestBuilding
            {
                Name = "Locked",
            }));
            World.Buffer<Prerequisite>(locked)
                .Add(new Prerequisite { TypeId = EntityInfo.TypeIdFromName("Never Built") });
            SetBuildOrder((barracks, 2), (soldier, 3), (locked, 1));
            World.AddResources(2, _supplies, 1000);

            World.Run(12f);

            Assert.AreEqual(2, Owned("Barracks").Count);
            Assert.AreEqual(3, Owned("Soldier").Count, "the unmet locked step keeps the AI from training freely");
            Assert.AreEqual(1000 - 2 * 50 - 3 * 10, World.GetResources(2, _supplies));
        }

        [Test]
        public void BuildOrder_SkipsUnaffordableStep_UntilItCanPay()
        {
            var (barracks, soldier) = BuildOrderBase();
            SpawnBuilder(new float3(0f, 0f, -6f), barracks);
            ExistingBarracks(soldier);
            SetBuildOrder((barracks, 2), (soldier, 1));
            World.AddResources(2, _supplies, 20);

            World.Run(2f);

            Assert.AreEqual(1, Owned("Barracks").Count, "too poor for the second barracks");
            Assert.AreEqual(1, Owned("Soldier").Count, "so it trained the affordable next step");
        }

        [Test]
        public void BuildOrder_SkipsStepAtItsBuildLimit_AndTrainsFreely()
        {
            var (barracks, soldier) = BuildOrderBase();
            SpawnBuilder(new float3(0f, 0f, -6f), barracks);
            var existing = ExistingBarracks(soldier);
            World.AddBuildLimit(barracks, 1);
            World.AddBuildLimit(existing, 1);
            SetBuildOrder((barracks, 2), (soldier, 1));
            World.AddResources(2, _supplies, 1000);

            World.Run(6f);

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
            World.CreateHints(data);
        }

        private Entity UnitPrefab(string name, int cost, float buildTime)
        {
            var prefab = World.MakePrefab(World.SpawnUnit(0, new float3(90f, 0f, 90f), new TestUnit { Name = name }));
            World.SetBuildTime(prefab, buildTime);
            World.SetCost(prefab, _supplies, cost);
            return prefab;
        }

        /// <summary>Slot 2's HQ, and a barracks prefab that trains the soldier prefab.</summary>
        private (Entity Barracks, Entity Soldier) BuildOrderBase()
        {
            World.SpawnBuilding(2, float3.zero, new float2(4f, 4f), new TestBuilding
            {
                Name = "HQ",
                PopulationProvided = 20,
            });
            var soldier = UnitPrefab("Soldier", 10, 0.5f);
            var barracks = World.MakePrefab(World.SpawnBuilding(0, float3.zero, new float2(4f, 4f), new TestBuilding
            {
                Complete = false,
                BuildTime = 1f,
                Name = "Barracks",
            }));
            World.SetCost(barracks, _supplies, 50);
            World.MakeProducer(barracks, new float3(0f, 0f, -3f), soldier);
            return (barracks, soldier);
        }

        private Entity ExistingBarracks(Entity soldier)
        {
            var existing = World.SpawnBuilding(2, new float3(20f, 0f, 20f), new float2(4f, 4f), new TestBuilding
            {
                Name = "Barracks",
            });
            return World.MakeProducer(existing, new float3(0f, 0f, -3f), soldier);
        }

        private void SpawnBuilder(float3 position, Entity barracks) =>
            World.MakeBuilder(World.SpawnUnit(2, position, new TestUnit { Name = "Builder" }), barracks);

        private void SetBuildOrder(params (Entity Prefab, int Count)[] steps)
        {
            var buffer = World.EntityManager.GetBuffer<AIBuildStep>(World.Player(2));
            foreach (var step in steps)
            {
                buffer.Add(new AIBuildStep { Prefab = step.Prefab, Count = step.Count });
            }
        }

        private List<Entity> Owned(string name) => World.Owned(EntityInfo.TypeIdFromName(name), 2);
    }
}
