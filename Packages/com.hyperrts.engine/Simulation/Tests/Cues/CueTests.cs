using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Cues.Voice;
using System.Collections.Generic;
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
    public class CueTests : SimulationFixture
    {
        private SoundCue _cue;
        private GameObject _rig;

        [SetUp]
        public void Prepare()
        {
            _cue = World.Own(ScriptableObject.CreateInstance<SoundCue>());
            _rig = World.Own(new GameObject("Rig"));
        }

        [Test]
        public void InstantWeapon_PlaysFireAtShooter_AndImpactAtTarget()
        {
            var shooter = Voiced(World.AddWeapon(World.SpawnUnit(1, float3.zero, name: "Rifle")), CueSlot.Fire,
                CueSlot.Impact);
            World.SpawnUnit(2, new float3(3f, 0f, 0f));

            var heard = World.QueuedOver(0.5f);

            var fire = Find(heard, CueSlot.Fire);
            Assert.AreEqual(World.GetTypeId(shooter), fire.TypeId);
            Assert.AreEqual(1, fire.Faction);
            Assert.AreEqual(float3.zero, fire.Position);
            Assert.AreEqual(new float3(3f, 0f, 0f), Find(heard, CueSlot.Impact).Position);
        }

        [Test]
        public void SilentEntities_WriteNoEvents()
        {
            World.AddWeapon(World.SpawnUnit(1, float3.zero));
            World.SpawnUnit(2, new float3(3f, 0f, 0f));

            Assert.IsEmpty(World.QueuedOver(3f));
        }

        [Test]
        public void EntitiesWithoutARigForTheSlot_PlayNothing()
        {
            Rigged(World.AddWeapon(World.SpawnUnit(1, float3.zero)), CueSlot.Death, float3.zero);
            World.AddWeapon(World.SpawnUnit(1, new float3(0f, 0f, 2f)));
            World.SpawnUnit(2, new float3(3f, 0f, 0f), maxHealth: 1000f);

            Assert.IsEmpty(World.QueuedOver(1f));
        }

        [Test]
        public void Events_LastOneFrame()
        {
            Voiced(World.AddWeapon(World.SpawnUnit(1, float3.zero), cooldown: 100f), CueSlot.Fire);
            World.SpawnUnit(2, new float3(3f, 0f, 0f));

            Assert.IsNotEmpty(World.QueuedOver(0.5f).FindAll(sound => sound.Slot == CueSlot.Fire));
            World.Tick();
            Assert.IsEmpty(World.Queued());
        }

        [TestCase("sound", TestName = "Death plays the victim's death sound")]
        [TestCase("effect", TestName = "Death plays the victim's death effect")]
        [TestCase("remains", TestName = "Death plays a cue for remains alone")]
        public void Death_PlaysTheVictimsDeathCue(string leaves)
        {
            World.AddWeapon(World.SpawnUnit(1, float3.zero), damage: 100f);
            var victim = World.SpawnUnit(2, new float3(3f, 0f, 0f), name: "Victim");
            switch (leaves)
            {
                case "sound":
                    Voiced(victim, CueSlot.Death);
                    break;
                case "effect":
                    Rigged(victim, CueSlot.Death, float3.zero);
                    break;
                default:
                    World.Add(victim, new DeathRemains { Radius = 2f, Amounts = 1f });
                    break;
            }

            var typeId = World.GetTypeId(victim);

            var death = Find(World.QueuedOver(1f), CueSlot.Death);

            Assert.AreEqual(typeId, death.TypeId);
            Assert.AreEqual(2, death.Faction);
            Assert.AreEqual(new float3(3f, 0f, 0f), death.Position);
        }

        [Test]
        public void Fire_PlaysAtTheMuzzle_FacingTheTarget()
        {
            var shooter = Rigged(World.AddWeapon(World.SpawnUnit(1, float3.zero, name: "Rifle")), CueSlot.Fire,
                new float3(0f, 1f, 2f));
            World.SpawnUnit(2, new float3(3f, 0f, 0f));

            var fire = Find(World.QueuedOver(0.5f), CueSlot.Fire);

            Assert.AreEqual(World.GetTypeId(shooter), fire.TypeId);
            Assert.AreEqual(1, fire.Faction);
            Assert.That(math.distance(new float3(2f, 1f, 0f), fire.Position), Is.LessThan(1e-3f),
                "the muzzle turns with the shooter");
            Assert.That(math.distance(new float3(1f, 0f, 0f), fire.Direction), Is.LessThan(1e-3f));
        }

        [Test]
        public void TurretFire_PlaysAtTheMuzzleTheTurretTurned()
        {
            var tank = World.AddWeapon(World.SpawnUnit(1, float3.zero, name: "Tank"), range: 10f,
                turreted: true);
            var weapon = World.Primary(tank);
            weapon.Aim.HasMuzzle = true;
            weapon.Aim.Muzzle = new float3(0f, 1f, 2f);
            World.SetPrimary(tank, weapon);
            Rigged(tank, CueSlot.Fire, weapon.Aim.Muzzle);
            World.SpawnUnit(2, new float3(8f, 0f, 0f), maxHealth: 1000f);

            var fire = Find(World.QueuedOver(1f), CueSlot.Fire);

            Assert.That(math.distance(new float3(2f, 1f, 0f), fire.Position), Is.LessThan(0.1f),
                "the hull stays put, its turret turns the muzzle onto the target");
        }

        [Test]
        public void EachWeapon_PlaysItsOwnFireCue_ByPoseIndex()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, cooldown: 100f);
            World.AddMount(gun, range: 6f, cooldown: 100f);
            var writer = World.Writer(gun);
            var sounds = EntitySoundsAuthoring.Add(writer);
            EntitySoundsAuthoring.Set(sounds, CueSlot.Fire, _cue);
            EntitySoundsAuthoring.Set(sounds, CueSlot.Fire, _cue, 1);
            World.SpawnUnit(2, new float3(4f, 0f, 0f));

            var fired = World.QueuedOver(1f, CueSlot.Fire).ConvertAll(cue => cue.Variant);

            CollectionAssert.AreEquivalent(new byte[] { 0, 1 }, fired);
        }

        [Test]
        public void ExtraWeaponWithoutAFireCue_FiresSilently()
        {
            var gun = Voiced(World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, cooldown: 100f),
                CueSlot.Fire);
            World.AddMount(gun, range: 6f, cooldown: 100f);
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f));

            var fired = World.QueuedOver(1f, CueSlot.Fire);

            Assert.Less(World.GetHealth(enemy), 75f, "both weapons fired");
            Assert.AreEqual(1, fired.Count, "the mount doesn't borrow the primary's cue");
            Assert.AreEqual(0, fired[0].Variant);
        }

        [Test]
        public void ProducedUnit_PlaysItsReadyCue()
        {
            var prefab = Voiced(World.MakePrefab(World.SpawnUnit(0, new float3(90f, 0f, 90f), name: "Tank")),
                CueSlot.Ready);
            World.SetBuildTime(prefab, 0.5f);
            var producer = World.MakeProducer(World.SpawnBuilding(1, float3.zero, new float2(4f, 4f)),
                new float3(0f, 0f, -4f), prefab);
            World.SpawnProvider(1, new float3(20f, 0f, 20f), 10);

            World.Produce(1, producer, prefab);
            var ready = Find(World.QueuedOver(1f), CueSlot.Ready);

            Assert.AreEqual(World.GetTypeId(prefab), ready.TypeId);
            Assert.AreEqual(1, ready.Faction);
            Assert.AreEqual(new float3(0f, 0f, -4f), ready.Position);
        }

        [Test]
        public void FinishedConstruction_PlaysTheBuildingsReadyCue()
        {
            var site = Voiced(World.SpawnBuilding(1, float3.zero, new float2(2f, 2f), complete: false, buildTime: 0.5f,
                name: "Depot"), CueSlot.Ready);
            var builder = World.MakeBuilder(World.SpawnUnit(1, new float3(2f, 0f, 0f)));
            World.Order(builder, OrderType.Build, site);

            Assert.AreEqual(World.GetTypeId(site), Find(World.QueuedOver(2f), CueSlot.Ready).TypeId);
        }

        [Test]
        public void MoveCommand_SelectedUnitAnswers_RateLimited()
        {
            var unit = Voiced(World.SpawnUnit(1, float3.zero, name: "Rifleman"), CueSlot.Move, CueSlot.Attack);
            World.SetEnabled<Selected>(unit, true);
            var move = new PlayerCommand { Type = CommandType.Move, Position = new float3(10f, 0f, 0f) };

            World.Command(1, move);
            World.Tick();
            Assert.AreEqual(CueSlot.Move, World.Queued()[0].Slot);
            Assert.AreEqual(World.GetTypeId(unit), World.Queued()[0].TypeId);

            World.Command(1, move);
            World.Tick();
            Assert.IsEmpty(World.Queued(), "spam-clicking within the interval stays quiet");

            World.Run(AcknowledgementSystem.Interval);
            var enemy = World.SpawnUnit(2, new float3(30f, 0f, 0f));
            World.Command(1, new PlayerCommand { Type = CommandType.Smart, Target = enemy });
            World.Tick();
            Assert.AreEqual(CueSlot.Attack, World.Queued()[0].Slot, "a smart click on an enemy is an attack");
        }

        [Test]
        public void Voices_ReachOnlyTheirOwner_WorldCuesWhoeverSeesThem()
        {
            World.SpawnUnit(2, new float3(50f, 0f, 50f));
            World.Tick();
            World.EntityManager.CompleteAllTrackedJobs();
            var fog = Single<FogOfWar>();
            var relations = Single<FactionRelations>();
            var voice = new CueEvent { Slot = CueSlot.Ready, Faction = 1 };
            var hiddenShot = new CueEvent { Slot = CueSlot.Fire, Faction = 1 };
            var seenShot = new CueEvent { Slot = CueSlot.Fire, Faction = 1, Position = new float3(52f, 0f, 50f) };

            Assert.IsTrue(CueRules.IsVisible(voice, 1, fog, relations));
            Assert.IsFalse(CueRules.IsVisible(voice, 2, fog, relations), "voices are private");
            Assert.IsFalse(CueRules.IsVisible(hiddenShot, 2, fog, relations), "hidden by fog");
            Assert.IsTrue(CueRules.IsVisible(seenShot, 2, fog, relations));
            seenShot.Concealment = Concealment.Stealthed;
            Assert.IsFalse(CueRules.IsVisible(seenShot, 2, fog, relations), "undetected stealth is silent");
            Assert.IsTrue(CueRules.IsVisible(hiddenShot, 1, fog, relations), "own cues always");
        }

        private Entity Voiced(Entity entity, params CueSlot[] slots)
        {
            var writer = World.Writer(entity);
            var sounds = EntitySoundsAuthoring.Add(writer);
            foreach (var slot in slots)
            {
                EntitySoundsAuthoring.Set(sounds, slot, _cue);
            }

            return entity;
        }

        private Entity Rigged(Entity entity, CueSlot slot, float3 socket)
        {
            var writer = World.Writer(entity);
            EntityEffectsAuthoring.Set(EntityEffectsAuthoring.Add(writer), slot, _rig, socket);
            return entity;
        }

        private static CueEvent Find(List<CueEvent> heard, CueSlot slot) => heard.Find(cue => cue.Slot == slot);

        private T Single<T>() where T : unmanaged, IComponentData
        {
            using var query = World.Query(typeof(T));
            return query.GetSingleton<T>();
        }
    }
}
