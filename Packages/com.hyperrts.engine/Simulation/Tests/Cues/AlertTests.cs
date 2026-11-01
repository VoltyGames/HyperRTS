using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Cues.Announcements;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Cues.Voice;
using System.Collections.Generic;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Cues
{
    /// <summary>Owner-only alerts: the ability and under fire voices, production done, and announcer lines gated per player.</summary>
    public class AlertTests : SimulationFixture
    {
        private static readonly float2 Footprint = new(4f, 4f);

        private SoundCue _cue;

        [SetUp]
        public void Prepare()
        {
            _cue = World.Own(ScriptableObject.CreateInstance<SoundCue>());
            var announcer = World.Writer(World.EntityManager.CreateEntity());
            AnnouncerAuthoring.Add(announcer);
        }

        [Test]
        public void Ability_SpeaksAtTheCaster_ForItsOwnerOnly()
        {
            var caster = Voiced(World.SpawnUnit(1, float3.zero), CueSlot.Ability, CueSlot.AbilityVoice);
            World.SpawnUnit(2, new float3(3f, 0f, 0f), speed: 0f);
            var id = EntityInfo.TypeIdFromName("Grenade");
            World.EntityManager.AddBuffer<Ability>(caster).Add(new Ability
            {
                Id = id, Name = "Grenade", Target = AbilityTarget.Point, Filter = AbilityTargetFilter.Hostile,
                Range = 5f, Cooldown = 20f, Damage = 10f,
            });
            World.Command(1, new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = caster, Position = new float3(3f, 0f, 0f), Argument = id,
            });

            var voices = World.QueuedOver(1f, CueSlot.AbilityVoice);

            Assert.AreEqual(1, voices.Count);
            Assert.AreEqual(float3.zero, voices[0].Position, "at the caster, not the aim point");
            Assert.IsTrue(CueRules.IsOwnerOnly(CueSlot.AbilityVoice));
            Assert.IsFalse(CueRules.IsOwnerOnly(CueSlot.Ability), "the world sound stays public");
        }

        [Test]
        public void UnderFire_CallsOutAfterAQuietSpell_Only()
        {
            var unit = Voiced(World.SpawnUnit(1, float3.zero, speed: 0f), CueSlot.UnderFire);

            Hit(unit);
            World.Tick();
            Assert.AreEqual(1, World.Queued(CueSlot.UnderFire).Length);
            Assert.AreEqual(1, World.Queued(CueSlot.UnderFire)[0].Faction);

            World.Run(UnderFireSystem.PlayerInterval + 1f);
            Hit(unit);
            World.Tick();
            Assert.IsEmpty(World.Queued(CueSlot.UnderFire), "still under fire, not a fresh alarm");

            World.Run(UnderFireVoice.QuietTime + 1f);
            Hit(unit);
            World.Tick();
            Assert.AreEqual(1, World.Queued(CueSlot.UnderFire).Length, "quiet long enough to call out again");
        }

        [Test]
        public void UnderFire_SpeaksOncePerPlayerInterval()
        {
            var a = Voiced(World.SpawnUnit(1, float3.zero, speed: 0f), CueSlot.UnderFire);
            var b = Voiced(World.SpawnUnit(1, new float3(5f, 0f, 0f), speed: 0f), CueSlot.UnderFire);
            var c = Voiced(World.SpawnUnit(1, new float3(10f, 0f, 0f), speed: 0f), CueSlot.UnderFire);

            Hit(a);
            Hit(b);
            World.Tick();
            Assert.AreEqual(1, World.Queued(CueSlot.UnderFire).Length);

            World.Run(UnderFireSystem.PlayerInterval + 0.1f);
            Hit(c);
            World.Tick();
            Assert.AreEqual(1, World.Queued(CueSlot.UnderFire).Length, "another unit, after the player's interval");
        }

        [Test]
        public void ProducedUnit_PlaysProductionDoneAtTheProducer()
        {
            var prefab = World.MakePrefab(World.SpawnUnit(0, new float3(90f, 0f, 90f), name: "Tank"));
            World.SetBuildTime(prefab, 0.5f);
            var producer = Voiced(World.MakeProducer(World.SpawnBuilding(1, new float3(4f, 0f, 4f),
                Footprint, name: "Factory"), new float3(0f, 0f, -4f), prefab), CueSlot.ProductionDone);
            World.SpawnProvider(1, new float3(20f, 0f, 20f), 10);

            World.Produce(1, producer, prefab);
            var done = World.QueuedOver(1f, CueSlot.ProductionDone);

            Assert.AreEqual(1, done.Count);
            Assert.AreEqual(World.GetTypeId(producer), done[0].TypeId);
            Assert.AreEqual(new float3(4f, 0f, 4f), done[0].Position);
            Assert.IsTrue(CueRules.IsOwnerOnly(CueSlot.ProductionDone));
        }

        [Test]
        public void WithoutAnAnnouncer_NothingIsAnnounced()
        {
            using var query = World.Query(typeof(AnnouncerVoice));
            World.EntityManager.DestroyEntity(query);
            var unit = World.SpawnUnit(1, new float3(3f, 0f, 0f));
            World.SpawnBuilding(1, new float3(-30f, 0f, -30f), Footprint);
            World.Kill(unit);

            Assert.IsEmpty(Announced(0.5f));
        }

        [Test]
        public void LostUnit_IsAnnouncedToItsOwnerOnly_WhoNeedsNoSoundOnIt()
        {
            var unit = World.SpawnUnit(1, new float3(3f, 0f, 0f), name: "Rifleman");
            World.SpawnBuilding(1, new float3(-30f, 0f, -30f), Footprint);
            World.SpawnBuilding(2, new float3(30f, 0f, 30f), Footprint);
            World.Kill(unit);

            var lost = Announced(0.5f, AnnouncementKind.UnitLost);

            Assert.AreEqual(1, lost.Count);
            Assert.AreEqual(1, lost[0].Faction);
            Assert.AreEqual(EntityInfo.TypeIdFromName("Rifleman"), lost[0].TypeId);
            Assert.IsTrue(CueRules.IsOwnerOnly(CueSlot.Announce));
        }

        [Test]
        public void BuildingHits_AreAnnouncedOncePerInterval()
        {
            var depot = World.SpawnBuilding(1, float3.zero, Footprint);
            var hit = new DamageEvent { Target = depot, SourceFaction = 2, Amount = 1f };

            var heard = new List<CueEvent>();
            for (var i = 0; i < 10; i++)
            {
                World.QueueDamage(hit);
                heard.AddRange(Announced(0.05f));
            }

            Assert.AreEqual(1, heard.FindAll(cue => cue.Announcement == AnnouncementKind.BaseUnderAttack).Count);
            World.Run(AnnouncementGate.Interval);
            World.QueueDamage(hit);
            Assert.AreEqual(AnnouncementKind.BaseUnderAttack, Announced(0.05f)[0].Announcement, "the gate reopens");
        }

        [Test]
        public void FinishedConstruction_IsAnnounced()
        {
            var site = World.SpawnBuilding(1, float3.zero, Footprint, complete: false, buildTime: 0.5f);
            var builder = World.MakeBuilder(World.SpawnUnit(1, new float3(3f, 0f, 0f)));
            World.Order(builder, OrderType.Build, site);

            var done = Announced(2f, AnnouncementKind.ConstructionComplete)[0];

            Assert.AreEqual(World.GetTypeId(site), done.TypeId);
        }

        [Test]
        public void ProducedUnit_AndFinishedResearch_AreAnnounced()
        {
            var tank = World.MakePrefab(World.SpawnUnit(0, new float3(90f, 0f, 90f), name: "Tank"));
            World.SetBuildTime(tank, 0.5f);
            var upgrade = Upgrade("Armor Plating");
            var factory = World.MakeProducer(World.SpawnBuilding(1, float3.zero, Footprint), new float3(0f, 0f, -4f),
                tank, upgrade);
            World.SpawnProvider(1, new float3(20f, 0f, 20f), 10);

            World.Produce(1, factory, tank);
            World.Produce(1, factory, upgrade);
            var heard = Announced(3f);

            Assert.AreEqual(EntityInfo.TypeIdFromName("Tank"),
                heard.Find(cue => cue.Announcement == AnnouncementKind.UnitReady).TypeId);
            Assert.AreEqual(EntityInfo.TypeIdFromName("Armor Plating"),
                heard.Find(cue => cue.Announcement == AnnouncementKind.UpgradeComplete).TypeId);
        }

        [Test]
        public void LowPower_IsAnnouncedWhenTheGridFallsShort()
        {
            World.SpawnBuilding(1, float3.zero, Footprint, power: 5f);
            Assert.IsEmpty(Announced(0.5f));

            World.SpawnBuilding(1, new float3(20f, 0f, 0f), Footprint, power: -10f);
            var low = Announced(1f, AnnouncementKind.LowPower);

            Assert.AreEqual(1, low.Count, "once, when it falls short");
            Assert.AreEqual(1, low[0].Faction);
        }

        [Test]
        public void FlickeringPower_IsAnnouncedOncePerInterval()
        {
            World.SpawnBuilding(1, float3.zero, Footprint, power: 5f);
            var drain = World.SpawnBuilding(1, new float3(20f, 0f, 0f), Footprint, power: -10f);
            var low = Announced(0.2f, AnnouncementKind.LowPower);
            World.Set(drain, new PowerSupply { Amount = 0f });
            low.AddRange(Announced(0.2f, AnnouncementKind.LowPower));
            World.Set(drain, new PowerSupply { Amount = -10f });
            low.AddRange(Announced(0.2f, AnnouncementKind.LowPower));

            Assert.AreEqual(1, low.Count, "the gate holds the second fall");
            World.Set(drain, new PowerSupply { Amount = 0f });
            World.Run(AnnouncementGate.Interval);
            World.Set(drain, new PowerSupply { Amount = -10f });
            Assert.AreEqual(1, Announced(0.2f, AnnouncementKind.LowPower).Count, "the gate reopens");
        }

        private Entity Voiced(Entity entity, params CueSlot[] slots)
        {
            var writer = World.Writer(entity);
            if (System.Array.IndexOf(slots, CueSlot.UnderFire) >= 0)
            {
                EntitySoundsAuthoring.AddUnderFire(writer);
            }

            var sounds = EntitySoundsAuthoring.Add(writer);
            foreach (var slot in slots)
            {
                EntitySoundsAuthoring.Set(sounds, slot, _cue);
            }

            return entity;
        }

        private void Hit(Entity target) =>
            World.QueueDamage(new DamageEvent { Target = target, SourceFaction = 2, Amount = 1f });

        private List<CueEvent> Announced(float seconds) => World.QueuedOver(seconds, CueSlot.Announce);

        private List<CueEvent> Announced(float seconds, AnnouncementKind kind) =>
            Announced(seconds).FindAll(cue => cue.Announcement == kind);

        private Entity Upgrade(string name)
        {
            var upgrade = World.EntityManager.CreateEntity();
            var writer = World.Writer(upgrade);
            var info = new EntityInfo { TypeId = EntityInfo.TypeIdFromName(name), Name = name };
            UpgradeAuthoring.Add(writer, info, 0.5f, new ResourceCost[0], new Prerequisite[0],
                new UpgradeEffect[0]);
            return World.MakePrefab(upgrade);
        }
    }
}
