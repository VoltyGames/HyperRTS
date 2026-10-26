using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Cues
{
    /// <summary>Sound and effect cues from weapons, deaths, production and acknowledgements, and who hears or sees them.</summary>
    public class CueTests
    {
        private TestWorld _world;
        private SoundCue _cue;
        private GameObject _rig;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _cue = _world.Own(ScriptableObject.CreateInstance<SoundCue>());
            _rig = _world.Own(new GameObject("Rig"));
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void InstantWeapon_PlaysFireAtShooter_AndImpactAtTarget()
        {
            var shooter = Voiced(_world.AddWeapon(_world.SpawnUnit(1, float3.zero, name: "Rifle")), CueSlot.Fire,
                CueSlot.Impact);
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            var heard = _world.QueuedOver(0.5f);

            var fire = Find(heard, CueSlot.Fire);
            Assert.AreEqual(_world.GetTypeId(shooter), fire.TypeId);
            Assert.AreEqual(1, fire.Faction);
            Assert.AreEqual(float3.zero, fire.Position);
            Assert.AreEqual(new float3(3f, 0f, 0f), Find(heard, CueSlot.Impact).Position);
        }

        [Test]
        public void SilentEntities_WriteNoEvents()
        {
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            Assert.IsEmpty(_world.QueuedOver(3f));
        }

        [Test]
        public void EntitiesWithoutARigForTheSlot_PlayNothing()
        {
            Rigged(_world.AddWeapon(_world.SpawnUnit(1, float3.zero)), CueSlot.Death, float3.zero);
            _world.AddWeapon(_world.SpawnUnit(1, new float3(0f, 0f, 2f)));
            _world.SpawnUnit(2, new float3(3f, 0f, 0f), maxHealth: 1000f);

            Assert.IsEmpty(_world.QueuedOver(1f));
        }

        [Test]
        public void Events_LastOneFrame()
        {
            Voiced(_world.AddWeapon(_world.SpawnUnit(1, float3.zero), cooldown: 100f), CueSlot.Fire);
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            Assert.IsNotEmpty(_world.QueuedOver(0.5f).FindAll(sound => sound.Slot == CueSlot.Fire));
            _world.Tick();
            Assert.IsEmpty(_world.Queued());
        }

        [TestCase(false, TestName = "Death plays the victim's death sound")]
        [TestCase(true, TestName = "Death plays the victim's death effect")]
        public void Death_PlaysTheVictimsDeathCue(bool effect)
        {
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero), damage: 100f);
            var victim = _world.SpawnUnit(2, new float3(3f, 0f, 0f), name: "Victim");
            if (effect)
            {
                Rigged(victim, CueSlot.Death, float3.zero);
            }
            else
            {
                Voiced(victim, CueSlot.Death);
            }

            var typeId = _world.GetTypeId(victim);

            var death = Find(_world.QueuedOver(1f), CueSlot.Death);

            Assert.AreEqual(typeId, death.TypeId);
            Assert.AreEqual(2, death.Faction);
            Assert.AreEqual(new float3(3f, 0f, 0f), death.Position);
        }

        [Test]
        public void Fire_PlaysAtTheMuzzle_FacingTheTarget()
        {
            var shooter = Rigged(_world.AddWeapon(_world.SpawnUnit(1, float3.zero, name: "Rifle")), CueSlot.Fire,
                new float3(0f, 1f, 2f));
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            var fire = Find(_world.QueuedOver(0.5f), CueSlot.Fire);

            Assert.AreEqual(_world.GetTypeId(shooter), fire.TypeId);
            Assert.AreEqual(1, fire.Faction);
            Assert.That(math.distance(new float3(2f, 1f, 0f), fire.Position), Is.LessThan(1e-3f),
                "the muzzle turns with the shooter");
            Assert.That(math.distance(new float3(1f, 0f, 0f), fire.Direction), Is.LessThan(1e-3f));
        }

        [Test]
        public void TurretFire_PlaysAtTheMuzzleTheTurretTurned()
        {
            var tank = _world.AddWeapon(_world.SpawnUnit(1, float3.zero, name: "Tank"), range: 10f,
                turreted: true);
            var weapon = _world.Get<Weapon>(tank);
            weapon.HasMuzzle = true;
            weapon.Muzzle = new float3(0f, 1f, 2f);
            _world.EntityManager.SetComponentData(tank, weapon);
            Rigged(tank, CueSlot.Fire, weapon.Muzzle);
            _world.SpawnUnit(2, new float3(8f, 0f, 0f), maxHealth: 1000f);

            var fire = Find(_world.QueuedOver(1f), CueSlot.Fire);

            Assert.That(math.distance(new float3(2f, 1f, 0f), fire.Position), Is.LessThan(0.1f),
                "the hull stays put, its turret turns the muzzle onto the target");
        }

        [Test]
        public void EachWeapon_PlaysItsOwnFireCue_ByPoseIndex()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, cooldown: 100f);
            _world.AddMount(gun, range: 6f, cooldown: 100f);
            var writer = new EntityManagerWriter(_world.EntityManager, gun);
            var sounds = EntitySoundsAuthoring.Add(ref writer);
            EntitySoundsAuthoring.Set(sounds, CueSlot.Fire, _cue);
            EntitySoundsAuthoring.Set(sounds, CueSlot.Fire, _cue, 1);
            _world.SpawnUnit(2, new float3(4f, 0f, 0f));

            var fired = _world.QueuedOver(1f, CueSlot.Fire).ConvertAll(cue => cue.Variant);

            CollectionAssert.AreEquivalent(new byte[] { 0, 1 }, fired);
        }

        [Test]
        public void ExtraWeaponWithoutAFireCue_FiresSilently()
        {
            var gun = Voiced(_world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, cooldown: 100f),
                CueSlot.Fire);
            _world.AddMount(gun, range: 6f, cooldown: 100f);
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f));

            var fired = _world.QueuedOver(1f, CueSlot.Fire);

            Assert.Less(_world.GetHealth(enemy), 75f, "both weapons fired");
            Assert.AreEqual(1, fired.Count, "the mount doesn't borrow the primary's cue");
            Assert.AreEqual(0, fired[0].Variant);
        }

        [Test]
        public void ProducedUnit_PlaysItsReadyCue()
        {
            var prefab = Voiced(_world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 90f), name: "Tank")),
                CueSlot.Ready);
            _world.SetBuildTime(prefab, 0.5f);
            var producer = _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f)),
                new float3(0f, 0f, -4f), prefab);
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);

            _world.Produce(1, producer, prefab);
            var ready = Find(_world.QueuedOver(1f), CueSlot.Ready);

            Assert.AreEqual(_world.GetTypeId(prefab), ready.TypeId);
            Assert.AreEqual(1, ready.Faction);
            Assert.AreEqual(new float3(0f, 0f, -4f), ready.Position);
        }

        [Test]
        public void FinishedConstruction_PlaysTheBuildingsReadyCue()
        {
            var site = Voiced(_world.SpawnBuilding(1, float3.zero, new float2(2f, 2f), complete: false, buildTime: 0.5f,
                name: "Depot"), CueSlot.Ready);
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, new float3(2f, 0f, 0f)));
            _world.Order(builder, OrderType.Build, site);

            Assert.AreEqual(_world.GetTypeId(site), Find(_world.QueuedOver(2f), CueSlot.Ready).TypeId);
        }

        [Test]
        public void MoveCommand_SelectedUnitAnswers_RateLimited()
        {
            var unit = Voiced(_world.SpawnUnit(1, float3.zero, name: "Rifleman"), CueSlot.Move, CueSlot.Attack);
            _world.EntityManager.SetComponentEnabled<Selected>(unit, true);
            var move = new PlayerCommand { Type = CommandType.Move, Position = new float3(10f, 0f, 0f) };

            _world.Command(1, move);
            _world.Tick();
            Assert.AreEqual(CueSlot.Move, _world.Queued()[0].Slot);
            Assert.AreEqual(_world.GetTypeId(unit), _world.Queued()[0].TypeId);

            _world.Command(1, move);
            _world.Tick();
            Assert.IsEmpty(_world.Queued(), "spam-clicking within the interval stays quiet");

            _world.Run(AcknowledgementSystem.Interval);
            var enemy = _world.SpawnUnit(2, new float3(30f, 0f, 0f));
            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Target = enemy });
            _world.Tick();
            Assert.AreEqual(CueSlot.Attack, _world.Queued()[0].Slot, "a smart click on an enemy is an attack");
        }

        [Test]
        public void Voices_ReachOnlyTheirOwner_WorldCuesWhoeverSeesThem()
        {
            _world.SpawnUnit(2, new float3(50f, 0f, 50f));
            _world.Tick();
            _world.EntityManager.CompleteAllTrackedJobs();
            var fog = Single<FogOfWar>();
            var relations = Single<FactionRelations>();
            var voice = new CueEvent { Slot = CueSlot.Ready, Faction = 1 };
            var hiddenShot = new CueEvent { Slot = CueSlot.Fire, Faction = 1 };
            var seenShot = new CueEvent { Slot = CueSlot.Fire, Faction = 1, Position = new float3(52f, 0f, 50f) };

            Assert.IsTrue(CueRules.IsVisible(voice, 1, fog, relations));
            Assert.IsFalse(CueRules.IsVisible(voice, 2, fog, relations), "voices are private");
            Assert.IsFalse(CueRules.IsVisible(hiddenShot, 2, fog, relations), "hidden by fog");
            Assert.IsTrue(CueRules.IsVisible(seenShot, 2, fog, relations));
            seenShot.Stealthed = true;
            Assert.IsFalse(CueRules.IsVisible(seenShot, 2, fog, relations), "undetected stealth is silent");
            Assert.IsTrue(CueRules.IsVisible(hiddenShot, 1, fog, relations), "own cues always");
        }

        [Test]
        public void VoicesPlayLocally_ForTheirOwnerOnly_WithoutAFogView()
        {
            var noFog = new LocalFogView { Viewer = 1, Active = false };
            var own = new CueEvent { Slot = CueSlot.Select, Faction = 1 };
            var other = new CueEvent { Slot = CueSlot.Select, Faction = 2 };

            Assert.IsTrue(CueRules.IsSeenLocally(own, noFog, default, default));
            Assert.IsFalse(CueRules.IsSeenLocally(other, noFog, default, default), "another player's voice stays private");
            Assert.IsTrue(CueRules.IsSeenLocally(new CueEvent { Slot = CueSlot.Fire, Faction = 2 }, noFog, default,
                default), "world cues play for all without fog");
        }

        private Entity Voiced(Entity entity, params CueSlot[] slots)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            var sounds = EntitySoundsAuthoring.Add(ref writer);
            foreach (var slot in slots)
            {
                EntitySoundsAuthoring.Set(sounds, slot, _cue);
            }

            return entity;
        }

        private Entity Rigged(Entity entity, CueSlot slot, float3 socket)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            EffectSetup.Set(EffectSetup.Add(ref writer), slot, _rig, socket);
            return entity;
        }

        private static CueEvent Find(List<CueEvent> heard, CueSlot slot) => heard.Find(cue => cue.Slot == slot);

        private T Single<T>() where T : unmanaged, IComponentData
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(T));
            return query.GetSingleton<T>();
        }
    }
}
