using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Cues.Voice;
using HyperRTS.Simulation.Vision.Detection;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using System.Collections.Generic;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Cues
{
    /// <summary>Sound and effect cues from weapons, deaths, production and acknowledgements, and who hears or sees them.</summary>
    public class CueTests : CueFixture
    {
        [Test]
        public void InstantWeapon_PlaysFireAtShooter_AndImpactAtTarget()
        {
            var shooter = Voiced(World.AddWeapon(World.SpawnUnit(1, float3.zero, name: "Rifle")), CueSlot.Fire,
                CueSlot.Impact);
            World.SpawnUnit(2, new float3(3f, 0f, 0f));

            var heard = World.QueuedOver(0.5f);

            var fire = Find(heard, CueSlot.Fire);
            Assert.AreEqual(World.GetTypeId(shooter), fire.TypeId);
            Assert.AreEqual(1, fire.Owner);
            Assert.AreEqual(float3.zero, fire.Position);
            Assert.AreEqual(new float3(3f, 0f, 0f), Find(heard, CueSlot.Impact).Position);
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
            Assert.AreEqual(2, death.Owner);
            Assert.AreEqual(new float3(3f, 0f, 0f), death.Position);
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
            Assert.AreEqual(1, ready.Owner);
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

            World.Run(World.Singleton<VoiceRules>().AcknowledgementInterval);
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
            var relations = Single<OwnerRelations>();
            var voice = new CueEvent { Slot = CueSlot.Ready, Owner = 1 };
            var hiddenShot = new CueEvent { Slot = CueSlot.Fire, Owner = 1 };
            var seenShot = new CueEvent { Slot = CueSlot.Fire, Owner = 1, Position = new float3(52f, 0f, 50f) };

            Assert.IsTrue(CueRules.IsVisible(voice, 1, fog, relations));
            Assert.IsFalse(CueRules.IsVisible(voice, 2, fog, relations), "voices are private");
            Assert.IsFalse(CueRules.IsVisible(hiddenShot, 2, fog, relations), "hidden by fog");
            Assert.IsTrue(CueRules.IsVisible(seenShot, 2, fog, relations));
            seenShot.Concealment = Concealment.Stealthed;
            Assert.IsFalse(CueRules.IsVisible(seenShot, 2, fog, relations), "undetected stealth is silent");
            Assert.IsTrue(CueRules.IsVisible(hiddenShot, 1, fog, relations), "own cues always");
        }
    }
}
