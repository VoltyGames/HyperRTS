using System.Collections.Generic;
using HyperRTS.Simulation.Audio;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Combat;
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

        private Entity Voiced(Entity entity, params SoundSlot[] slots)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            var sounds = SoundSetup.Add(ref writer);
            foreach (var slot in slots)
            {
                SoundSetup.Set(sounds, slot, _cue);
            }

            return entity;
        }

        private SoundEvent[] Queued()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(SoundQueue));
            var buffer = _world.EntityManager.GetBuffer<SoundEvent>(query.GetSingletonEntity());
            return buffer.AsNativeArray().ToArray();
        }

        /// <summary>Every event of the next frames; the queue itself only holds the last frame's.</summary>
        private List<SoundEvent> Heard(float seconds)
        {
            var heard = new List<SoundEvent>();
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                _world.Tick();
                heard.AddRange(Queued());
            }

            return heard;
        }

        private static SoundEvent Find(List<SoundEvent> heard, SoundSlot slot) =>
            heard.Find(sound => sound.Slot == slot);

        private int TypeIdOf(Entity entity) => _world.Get<EntityInfo>(entity).TypeId;

        [Test]
        public void InstantWeapon_PlaysFireAtShooter_AndImpactAtTarget()
        {
            var shooter = Voiced(_world.AddWeapon(_world.SpawnUnit(1, float3.zero, name: "Rifle")), SoundSlot.Fire,
                SoundSlot.Impact);
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            var heard = Heard(0.5f);

            var fire = Find(heard, SoundSlot.Fire);
            Assert.AreEqual(TypeIdOf(shooter), fire.TypeId);
            Assert.AreEqual(1, fire.Faction);
            Assert.AreEqual(float3.zero, fire.Position);
            Assert.AreEqual(new float3(3f, 0f, 0f), Find(heard, SoundSlot.Impact).Position);
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
                cooldown: 100f, projectile: bullet), SoundSlot.Impact);
            var typeId = TypeIdOf(shooter);
            _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            using var projectiles = _world.EntityManager.CreateEntityQuery(typeof(Projectile));
            for (var frame = 0; frame < 30 && projectiles.IsEmpty; frame++)
            {
                _world.Tick();
            }

            _world.EntityManager.DestroyEntity(shooter);
            var impact = Find(Heard(2f), SoundSlot.Impact);

            Assert.AreEqual(typeId, impact.TypeId);
            Assert.AreEqual(6f, impact.Position.x, 0.5f);
        }

        [Test]
        public void Death_PlaysTheVictimsDeathCue()
        {
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero), damage: 100f);
            var victim = Voiced(_world.SpawnUnit(2, new float3(3f, 0f, 0f), name: "Victim"), SoundSlot.Death);
            var typeId = TypeIdOf(victim);

            var death = Find(Heard(1f), SoundSlot.Death);

            Assert.AreEqual(typeId, death.TypeId);
            Assert.AreEqual(2, death.Faction);
            Assert.AreEqual(new float3(3f, 0f, 0f), death.Position);
        }

        [Test]
        public void ProducedUnit_PlaysItsReadyCue()
        {
            var prefab = Voiced(_world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 90f), name: "Tank")),
                SoundSlot.Ready);
            _world.SetBuildTime(prefab, 0.5f);
            var producer = _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f)),
                new float3(0f, 0f, -4f), prefab);
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);

            _world.Produce(1, producer, prefab);
            var ready = Find(Heard(1f), SoundSlot.Ready);

            Assert.AreEqual(TypeIdOf(prefab), ready.TypeId);
            Assert.AreEqual(1, ready.Faction);
            Assert.AreEqual(new float3(0f, 0f, -4f), ready.Position);
        }

        [Test]
        public void FinishedConstruction_PlaysTheBuildingsReadyCue()
        {
            var site = Voiced(_world.SpawnBuilding(1, float3.zero, new float2(2f, 2f), complete: false, buildTime: 0.5f,
                name: "Depot"), SoundSlot.Ready);
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, new float3(2f, 0f, 0f)));
            _world.Order(builder, OrderType.Build, site);

            Assert.AreEqual(TypeIdOf(site), Find(Heard(2f), SoundSlot.Ready).TypeId);
        }

        [Test]
        public void Events_LastOneFrame()
        {
            Voiced(_world.AddWeapon(_world.SpawnUnit(1, float3.zero), cooldown: 100f), SoundSlot.Fire);
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            Assert.IsNotEmpty(Heard(0.5f).FindAll(sound => sound.Slot == SoundSlot.Fire));
            _world.Tick();
            Assert.IsEmpty(Queued());
        }

        [Test]
        public void MoveCommand_SelectedUnitAnswers_RateLimited()
        {
            var unit = Voiced(_world.SpawnUnit(1, float3.zero, name: "Rifleman"), SoundSlot.Move, SoundSlot.Attack);
            _world.EntityManager.SetComponentEnabled<Selected>(unit, true);
            var move = new PlayerCommand { Type = CommandType.Move, Position = new float3(10f, 0f, 0f) };

            _world.Command(1, move);
            _world.Tick();
            Assert.AreEqual(SoundSlot.Move, Queued()[0].Slot);
            Assert.AreEqual(TypeIdOf(unit), Queued()[0].TypeId);

            _world.Command(1, move);
            _world.Tick();
            Assert.IsEmpty(Queued(), "spam-clicking within the interval stays quiet");

            _world.Run(AcknowledgementSystem.Interval);
            var enemy = _world.SpawnUnit(2, new float3(30f, 0f, 0f));
            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Target = enemy });
            _world.Tick();
            Assert.AreEqual(SoundSlot.Attack, Queued()[0].Slot, "a smart click on an enemy is an attack");
        }

        [Test]
        public void Voices_ReachOnlyTheirOwner_WorldSoundsWhoeverSeesThem()
        {
            _world.SpawnUnit(2, new float3(50f, 0f, 50f));
            _world.Tick();
            _world.EntityManager.CompleteAllTrackedJobs();
            var fog = Single<FogOfWar>();
            var relations = Single<FactionRelations>();
            var voice = new SoundEvent { Slot = SoundSlot.Ready, Faction = 1 };
            var hiddenShot = new SoundEvent { Slot = SoundSlot.Fire, Faction = 1 };
            var seenShot = new SoundEvent { Slot = SoundSlot.Fire, Faction = 1, Position = new float3(52f, 0f, 50f) };

            Assert.IsTrue(SoundRules.IsAudible(voice, 1, fog, relations));
            Assert.IsFalse(SoundRules.IsAudible(voice, 2, fog, relations), "voices are private");
            Assert.IsFalse(SoundRules.IsAudible(hiddenShot, 2, fog, relations), "hidden by fog");
            Assert.IsTrue(SoundRules.IsAudible(seenShot, 2, fog, relations));
            seenShot.Stealthed = true;
            Assert.IsFalse(SoundRules.IsAudible(seenShot, 2, fog, relations), "undetected stealth is silent");
            Assert.IsTrue(SoundRules.IsAudible(hiddenShot, 1, fog, relations), "own sounds always");
        }

        private T Single<T>() where T : unmanaged, IComponentData
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(T));
            return query.GetSingleton<T>();
        }
    }
}
