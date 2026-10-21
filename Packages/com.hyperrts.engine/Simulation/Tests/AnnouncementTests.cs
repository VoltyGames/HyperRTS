using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Announcer alerts written from gameplay events, owner only and gated per player.</summary>
    public class AnnouncementTests
    {
        private static readonly float2 Footprint = new(4f, 4f);

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            var announcer = new EntityManagerWriter(_world.EntityManager, _world.EntityManager.CreateEntity());
            AnnouncerAuthoring.Add(ref announcer);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private List<CueEvent> Announced(float seconds) =>
            _world.QueuedOver(seconds).FindAll(cue => CueRules.IsAnnouncement(cue.Slot));

        [Test]
        public void Announcements_AreOwnerOnly()
        {
            for (var slot = CueSlot.AnnounceUnitLost; slot <= CueSlot.AnnounceUpgradeComplete; slot++)
            {
                Assert.IsTrue(CueRules.IsOwnerOnly(slot), slot.ToString());
            }

            Assert.IsFalse(CueRules.IsAnnouncement(CueSlot.UnderFire));
        }

        [Test]
        public void WithoutAnAnnouncer_NothingIsAnnounced()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(AnnouncerVoice));
            _world.EntityManager.DestroyEntity(query);
            var unit = _world.SpawnUnit(1, new float3(3f, 0f, 0f));
            _world.SpawnBuilding(1, new float3(-30f, 0f, -30f), Footprint);
            _world.Kill(unit);

            Assert.IsEmpty(Announced(0.5f));
        }

        [Test]
        public void LostUnit_IsAnnouncedToItsOwner_WhoNeedsNoSoundOnIt()
        {
            var unit = _world.SpawnUnit(1, new float3(3f, 0f, 0f), name: "Rifleman");
            _world.SpawnBuilding(1, new float3(-30f, 0f, -30f), Footprint);
            _world.SpawnBuilding(2, new float3(30f, 0f, 30f), Footprint);
            _world.Kill(unit);

            var lost = Announced(0.5f).FindAll(cue => cue.Slot == CueSlot.AnnounceUnitLost);

            Assert.AreEqual(1, lost.Count);
            Assert.AreEqual(1, lost[0].Faction);
            Assert.AreEqual(EntityInfo.TypeIdFromName("Rifleman"), lost[0].TypeId);
        }

        [Test]
        public void BuildingHits_AreAnnouncedOncePerInterval()
        {
            var depot = _world.SpawnBuilding(1, float3.zero, Footprint);
            var hit = new DamageEvent { Target = depot, SourceFaction = 2, Amount = 1f };

            var heard = new List<CueEvent>();
            for (var i = 0; i < 10; i++)
            {
                _world.QueueDamage(hit);
                heard.AddRange(Announced(0.05f));
            }

            Assert.AreEqual(1, heard.FindAll(cue => cue.Slot == CueSlot.AnnounceBaseUnderAttack).Count);
            _world.Run(AnnouncementGate.Interval);
            _world.QueueDamage(hit);
            Assert.AreEqual(CueSlot.AnnounceBaseUnderAttack, Announced(0.05f)[0].Slot, "the gate reopens");
        }

        [Test]
        public void FinishedConstruction_IsAnnounced()
        {
            var site = _world.SpawnBuilding(1, float3.zero, Footprint, complete: false, buildTime: 0.5f);
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, new float3(3f, 0f, 0f)));
            _world.Order(builder, OrderType.Build, site);

            var done = Announced(2f).Find(cue => cue.Slot == CueSlot.AnnounceConstructionComplete);

            Assert.AreEqual(_world.GetTypeId(site), done.TypeId);
        }

        [Test]
        public void ProducedUnit_AndFinishedResearch_AreAnnounced()
        {
            var tank = _world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 90f), name: "Tank"));
            _world.SetBuildTime(tank, 0.5f);
            var upgrade = Upgrade("Armor Plating");
            var factory = _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, Footprint), new float3(0f, 0f, -4f),
                tank, upgrade);
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);

            _world.Produce(1, factory, tank);
            _world.Produce(1, factory, upgrade);
            var heard = Announced(3f);

            Assert.AreEqual(EntityInfo.TypeIdFromName("Tank"),
                heard.Find(cue => cue.Slot == CueSlot.AnnounceUnitReady).TypeId);
            Assert.AreEqual(EntityInfo.TypeIdFromName("Armor Plating"),
                heard.Find(cue => cue.Slot == CueSlot.AnnounceUpgradeComplete).TypeId);
        }

        [Test]
        public void LowPower_IsAnnouncedWhenTheGridFallsShort()
        {
            _world.SpawnBuilding(1, float3.zero, Footprint, power: 5f);
            Assert.IsEmpty(Announced(0.5f));

            _world.SpawnBuilding(1, new float3(20f, 0f, 0f), Footprint, power: -10f);
            var low = Announced(1f).FindAll(cue => cue.Slot == CueSlot.AnnounceLowPower);

            Assert.AreEqual(1, low.Count, "once, when it falls short");
            Assert.AreEqual(1, low[0].Faction);
        }

        private Entity Upgrade(string name)
        {
            var upgrade = _world.EntityManager.CreateEntity();
            var writer = new EntityManagerWriter(_world.EntityManager, upgrade);
            var info = new EntityInfo { TypeId = EntityInfo.TypeIdFromName(name), Name = name };
            UpgradeAuthoring.Add(ref writer, info, 0.5f, new ResourceCost[0], new Prerequisite[0],
                new UpgradeEffect[0]);
            return _world.MakePrefab(upgrade);
        }
    }
}
