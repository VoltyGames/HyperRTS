using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>Why the command card refuses a purchase, and what it reads from selected producers' queues.</summary>
    public class PurchaseCommandsTests
    {
        private static readonly float3 Far = new(60f, 0f, 60f);

        private TestWorld _world;
        private ResourceType _supplies;
        private MatchView _view;
        private HUDContext _context;
        private HUDText _text;

        [SetUp]
        public void SetUp()
        {
            // A game's translated HUDText outlives Play mode (no domain reload); these tests read the engine's keys.
            _text = HUDText.Current;
            HUDText.Current = new HUDText();
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = _world.CreateSupplies();
            _view = new MatchView();
            _context = new HUDContext();
        }

        [TearDown]
        public void TearDown()
        {
            HUDText.Current = _text;
            _world.Dispose();
        }

        private EntityManager EntityManager => _world.EntityManager;

        [Test]
        public void FindRefusal_NamesAMissingPrerequisite_BeforeThePrice()
        {
            var prefab = _world.UnitPrefab(_supplies, 100);
            Require(prefab, "Barracks");

            Assert.AreEqual(PurchaseRefusal.MissingPrerequisite, Refusal(prefab), "locked and unaffordable");

            _world.SpawnBuilding(1, Far, new float2(4f, 4f), name: "Barracks");
            Assert.AreEqual(PurchaseRefusal.CannotAfford, Refusal(prefab));

            _world.AddResources(1, _supplies, 100);
            Assert.AreEqual(PurchaseRefusal.None, Refusal(prefab));
        }

        [Test]
        public void FindRefusal_NamesAReachedLimit_AndFinishedResearch()
        {
            _world.AddResources(1, _supplies, 1000);
            var hero = _world.AddBuildLimit(_world.UnitPrefab(_supplies, 10, name: "Hero"), 1);
            _world.AddBuildLimit(_world.SpawnUnit(1, Far, name: "Hero"), 1);
            var upgrade = _world.UnitPrefab(_supplies, 10, name: "Upgrade");
            EntityManager.AddComponent<Research>(upgrade);
            EntityManager.GetBuffer<CompletedResearch>(_world.Player(1)).Add(new CompletedResearch
            {
                Prefab = upgrade, TypeId = EntityInfo.TypeIdFromName("Upgrade"),
            });

            Assert.AreEqual(PurchaseRefusal.AtLimit, Refusal(hero));
            Assert.AreEqual(PurchaseRefusal.Researched, Refusal(upgrade));
        }

        [Test]
        public void FindMissingPrerequisites_ListsOnlyUnmetTypesWithAPrefab()
        {
            var prefab = _world.UnitPrefab(_supplies, 10);
            var radar = _world.MakePrefab(_world.SpawnBuilding(0, Far, new float2(4f, 4f), name: "Radar"));
            Require(prefab, "Barracks");
            Require(prefab, "Radar");
            Require(prefab, "Never Made");
            _world.SpawnBuilding(1, Far, new float2(4f, 4f), name: "Barracks");
            _world.Tick();
            Refresh();

            var missing = new List<Entity>();
            _context.FindMissingPrerequisites(prefab, _context.SnapshotBuyer(), missing);

            CollectionAssert.AreEqual(new[] { radar }, missing);
        }

        [Test]
        public void Card_CountsEveryQueuedCopy_AndSweepsTheFurthestHead()
        {
            var tank = _world.UnitPrefab(_supplies, 10, buildTime: 10f, name: "Tank");
            var jeep = _world.UnitPrefab(_supplies, 10, buildTime: 10f, name: "Jeep");
            var first = Queue(SpawnProducer(tank, jeep), 2.5f, tank, tank, jeep);
            var second = Queue(SpawnProducer(tank, jeep), 5f, tank);
            Refresh();

            var source = new PurchaseCommands();
            var entries = new List<CommandEntry>();
            source.Build(_context, new[] { first, second }, entries);
            source.Refresh(_context);
            var tanks = entries[0].Button;
            var jeeps = entries[1].Button;

            Assert.AreEqual("3", tanks.Q<Label>(className: "hud-command__count").text);
            Assert.AreEqual(0.5f, tanks.Q<HUDSweep>().Fraction, 1e-5f);
            Assert.AreEqual("1", jeeps.Q<Label>(className: "hud-command__count").text);
            Assert.IsTrue(jeeps.Q<HUDSweep>().ClassListContains("hrts-hidden"), "queued behind a tank");
        }

        [Test]
        public void Card_ShowsRefusalQueueAndLowPower_AndIgnoresPressesWhileRefused()
        {
            var tank = _world.UnitPrefab(_supplies, 100, buildTime: 10f, name: "Tank");
            var producer = Queue(_world.SpawnProducer(tank), 5f, tank);
            var offline = _world.Get<Offline>(producer);
            offline.Set(OfflineReason.Unpowered, true);
            EntityManager.SetComponentData(producer, offline);
            EntityManager.SetComponentEnabled<Offline>(producer, true);
            Refresh();

            var source = new PurchaseCommands();
            var entries = new List<CommandEntry>();
            source.Build(_context, new[] { producer }, entries);
            source.Refresh(_context);
            var button = entries.Single().Button;

            Assert.IsTrue(button.ClassListContains("hud-command--unaffordable"));
            Assert.IsFalse(button.CanPress);
            Assert.AreEqual("1", button.Q<Label>(className: "hud-command__count").text);
            Assert.IsFalse(button.Q<HUDSweep>().ClassListContains("hrts-hidden"), "the head is in work");
            Assert.IsFalse(button.Q<HUDGlyph>(className: "hud-command__unpowered").ClassListContains("hrts-hidden"));

            _world.AddResources(1, _supplies, 100);
            var version = button.TooltipVersion;
            source.Refresh(_context);

            Assert.IsTrue(button.CanPress);
            Assert.AreNotEqual(version, button.TooltipVersion, "the tooltip refills when the verdict changes");
        }

        [Test]
        public void Tooltip_ShowsTheNameAndMissingPrerequisitesAsWarnings()
        {
            var prefab = _world.UnitPrefab(_supplies, 10, name: "Tank");
            _world.MakePrefab(_world.SpawnBuilding(0, Far, new float2(4f, 4f), name: "Factory"));
            Require(prefab, "Factory");
            var builder = _world.SpawnUnit(1, Far);
            _world.MakeBuilder(builder, prefab);
            _world.Tick();
            Refresh();

            var entries = new List<CommandEntry>();
            new PurchaseCommands().Build(_context, new[] { builder }, entries);
            var tooltip = new HUDTooltip();
            Assert.IsTrue(entries.Single().Button.Describe(tooltip));

            Assert.AreEqual("Tank", tooltip.Q<Label>(className: "hud-tooltip__title").text);
            var warning = tooltip.Q<Label>(className: "hud-tooltip__line--warning");
            Assert.AreEqual("Requires Factory", warning.text);
        }

        [Test]
        public void AdvancedBuildOptions_GoOnTheSecondPage_OncePerPrefab()
        {
            var depot = _world.UnitPrefab(_supplies, 10, name: "Depot");
            var airfield = _world.UnitPrefab(_supplies, 10, name: "Airfield");
            var first = _world.AddAdvancedBuildOptions(_world.MakeBuilder(_world.SpawnUnit(1, Far), depot), airfield);
            var second = _world.MakeBuilder(_world.SpawnUnit(1, Far), airfield);
            Refresh();

            var entries = new List<CommandEntry>();
            new PurchaseCommands().Build(_context, new[] { first, second }, entries);

            CollectionAssert.AreEqual(new[] { 0, 1 }, entries.Select(entry => entry.Page));
        }

        private Entity SpawnProducer(params Entity[] options) =>
            _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), new float3(0f, 0f, -4f),
                options);

        private void Require(Entity prefab, string type) =>
            EntityManager.GetBuffer<Prerequisite>(prefab)
                .Add(new Prerequisite { TypeId = EntityInfo.TypeIdFromName(type) });

        private Entity Queue(Entity producer, float elapsed, params Entity[] prefabs)
        {
            foreach (var prefab in prefabs)
            {
                EntityManager.GetBuffer<ProductionQueueItem>(producer).Add(new ProductionQueueItem
                {
                    Prefab = prefab, TypeId = EntityManager.GetComponentData<EntityInfo>(prefab).TypeId,
                });
            }

            var state = _world.Get<Producer>(producer);
            state.Elapsed = elapsed;
            EntityManager.SetComponentData(producer, state);
            return producer;
        }

        private PurchaseRefusal Refusal(Entity prefab)
        {
            Refresh();
            return _context.FindRefusal(prefab, _context.SnapshotBuyer());
        }

        private void Refresh()
        {
            _view.Refresh(_world.World);
            Assert.IsTrue(_context.Refresh(_view));
        }
    }
}
