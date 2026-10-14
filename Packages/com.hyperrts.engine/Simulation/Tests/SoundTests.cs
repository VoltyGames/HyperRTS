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
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Sound events from weapons, deaths, production and voice acknowledgements, run through the systems.</summary>
    public class SoundTests
    {
        private TestWorld _world;
        private SoundCue _cue;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _cue = ScriptableObject.CreateInstance<SoundCue>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_cue);
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

        private CueEvent[] Queued()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(CueQueue));
            var buffer = _world.EntityManager.GetBuffer<CueEvent>(query.GetSingletonEntity());
            return buffer.AsNativeArray().ToArray();
        }

        /// <summary>Every event of the next frames; the queue itself only holds the last frame's.</summary>
        private List<CueEvent> Heard(float seconds)
        {
            var heard = new List<CueEvent>();
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                _world.Tick();
                heard.AddRange(Queued());
            }

            return heard;
        }

        private static CueEvent Find(List<CueEvent> heard, CueSlot slot) =>
            heard.Find(sound => sound.Slot == slot);

        private int GetTypeId(Entity entity) => _world.Get<EntityInfo>(entity).TypeId;

        [Test]
        public void InstantWeapon_PlaysFireAtShooter_AndImpactAtTarget()
        {
            var shooter = Voiced(_world.AddWeapon(_world.SpawnUnit(1, float3.zero, name: "Rifle")), CueSlot.Fire,
                CueSlot.Impact);
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            var heard = Heard(0.5f);

            var fire = Find(heard, CueSlot.Fire);
            Assert.AreEqual(GetTypeId(shooter), fire.TypeId);
            Assert.AreEqual(1, fire.Faction);
            Assert.AreEqual(float3.zero, fire.Position);
            Assert.AreEqual(new float3(3f, 0f, 0f), Find(heard, CueSlot.Impact).Position);
        }

        [Test]
        public void SilentEntities_WriteNoEvents()
        {
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            Assert.IsEmpty(Heard(3f));
        }

        [Test]
        public void ProjectileImpact_PlaysOnArrival_EvenAfterTheShooterDied()
        {
            var bullet = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            _world.EntityManager.SetComponentData(bullet, LocalTransform.Identity);
            var shooter = Voiced(_world.AddWeapon(_world.SpawnUnit(1, float3.zero, name: "Mortar"), range: 8f,
                cooldown: 100f, projectile: bullet), CueSlot.Impact);
            var typeId = GetTypeId(shooter);
            _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            using var projectiles = _world.EntityManager.CreateEntityQuery(typeof(Projectile));
            for (var frame = 0; frame < 30 && projectiles.IsEmpty; frame++)
            {
                _world.Tick();
            }

            _world.EntityManager.DestroyEntity(shooter);
            var impact = Find(Heard(2f), CueSlot.Impact);

            Assert.AreEqual(typeId, impact.TypeId);
            Assert.AreEqual(6f, impact.Position.x, 0.5f);
        }

        [Test]
        public void Death_PlaysTheVictimsDeathCue()
        {
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero), damage: 100f);
            var victim = Voiced(_world.SpawnUnit(2, new float3(3f, 0f, 0f), name: "Victim"), CueSlot.Death);
            var typeId = GetTypeId(victim);

            var death = Find(Heard(1f), CueSlot.Death);

            Assert.AreEqual(typeId, death.TypeId);
            Assert.AreEqual(2, death.Faction);
            Assert.AreEqual(new float3(3f, 0f, 0f), death.Position);
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
            var ready = Find(Heard(1f), CueSlot.Ready);

            Assert.AreEqual(GetTypeId(prefab), ready.TypeId);
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

            Assert.AreEqual(GetTypeId(site), Find(Heard(2f), CueSlot.Ready).TypeId);
        }

        [Test]
        public void Events_LastOneFrame()
        {
            Voiced(_world.AddWeapon(_world.SpawnUnit(1, float3.zero), cooldown: 100f), CueSlot.Fire);
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            Assert.IsNotEmpty(Heard(0.5f).FindAll(sound => sound.Slot == CueSlot.Fire));
            _world.Tick();
            Assert.IsEmpty(Queued());
        }

        [Test]
        public void MoveCommand_SelectedUnitAnswers_RateLimited()
        {
            var unit = Voiced(_world.SpawnUnit(1, float3.zero, name: "Rifleman"), CueSlot.Move, CueSlot.Attack);
            _world.EntityManager.SetComponentEnabled<Selected>(unit, true);
            var move = new PlayerCommand { Type = CommandType.Move, Position = new float3(10f, 0f, 0f) };

            _world.Command(1, move);
            _world.Tick();
            Assert.AreEqual(CueSlot.Move, Queued()[0].Slot);
            Assert.AreEqual(GetTypeId(unit), Queued()[0].TypeId);

            _world.Command(1, move);
            _world.Tick();
            Assert.IsEmpty(Queued(), "spam-clicking within the interval stays quiet");

            _world.Run(AcknowledgementSystem.Interval);
            var enemy = _world.SpawnUnit(2, new float3(30f, 0f, 0f));
            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Target = enemy });
            _world.Tick();
            Assert.AreEqual(CueSlot.Attack, Queued()[0].Slot, "a smart click on an enemy is an attack");
        }

        [Test]
        public void Voices_ReachOnlyTheirOwner_WorldSoundsWhoeverSeesThem()
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
            Assert.IsTrue(CueRules.IsVisible(hiddenShot, 1, fog, relations), "own sounds always");
        }

        private T Single<T>() where T : unmanaged, IComponentData
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(T));
            return query.GetSingleton<T>();
        }
    }
}
