using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Owner-only cues: the ability voice, the under fire voice and production done.</summary>
    public class VoiceAlertTests
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
            if (System.Array.IndexOf(slots, CueSlot.UnderFire) >= 0)
            {
                EntitySoundsAuthoring.AddUnderFire(ref writer);
            }

            var sounds = EntitySoundsAuthoring.Add(ref writer);
            foreach (var slot in slots)
            {
                EntitySoundsAuthoring.Set(sounds, slot, _cue);
            }

            return entity;
        }

        private void Hit(Entity target) =>
            _world.QueueDamage(new DamageEvent { Target = target, SourceFaction = 2, Amount = 1f });

        [Test]
        public void Ability_SpeaksAtTheCaster_ForItsOwnerOnly()
        {
            var caster = Voiced(_world.SpawnUnit(1, float3.zero), CueSlot.Ability, CueSlot.AbilityVoice);
            _world.SpawnUnit(2, new float3(3f, 0f, 0f), speed: 0f);
            var id = EntityInfo.TypeIdFromName("Grenade");
            _world.EntityManager.AddBuffer<Ability>(caster).Add(new Ability
            {
                Id = id, Name = "Grenade", Target = AbilityTarget.Point, Filter = AbilityTargetFilter.Hostile,
                Range = 5f, Cooldown = 20f, Damage = 10f,
            });
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = caster, Position = new float3(3f, 0f, 0f), Argument = id,
            });

            var voices = _world.QueuedOver(1f, CueSlot.AbilityVoice);

            Assert.AreEqual(1, voices.Count);
            Assert.AreEqual(float3.zero, voices[0].Position, "at the caster, not the aim point");
            Assert.IsTrue(CueRules.IsOwnerOnly(CueSlot.AbilityVoice));
            Assert.IsFalse(CueRules.IsOwnerOnly(CueSlot.Ability), "the world sound stays public");
        }

        [Test]
        public void UnderFire_CallsOutAfterAQuietSpell_Only()
        {
            var unit = Voiced(_world.SpawnUnit(1, float3.zero, speed: 0f), CueSlot.UnderFire);

            Hit(unit);
            _world.Tick();
            Assert.AreEqual(1, _world.Queued(CueSlot.UnderFire).Length);
            Assert.AreEqual(1, _world.Queued(CueSlot.UnderFire)[0].Faction);

            _world.Run(UnderFireSystem.PlayerInterval + 1f);
            Hit(unit);
            _world.Tick();
            Assert.IsEmpty(_world.Queued(CueSlot.UnderFire), "still under fire, not a fresh alarm");

            _world.Run(UnderFireVoice.QuietTime + 1f);
            Hit(unit);
            _world.Tick();
            Assert.AreEqual(1, _world.Queued(CueSlot.UnderFire).Length, "quiet long enough to call out again");
        }

        [Test]
        public void UnderFire_SpeaksOncePerPlayerInterval()
        {
            var a = Voiced(_world.SpawnUnit(1, float3.zero, speed: 0f), CueSlot.UnderFire);
            var b = Voiced(_world.SpawnUnit(1, new float3(5f, 0f, 0f), speed: 0f), CueSlot.UnderFire);
            var c = Voiced(_world.SpawnUnit(1, new float3(10f, 0f, 0f), speed: 0f), CueSlot.UnderFire);

            Hit(a);
            Hit(b);
            _world.Tick();
            Assert.AreEqual(1, _world.Queued(CueSlot.UnderFire).Length);

            _world.Run(UnderFireSystem.PlayerInterval + 0.1f);
            Hit(c);
            _world.Tick();
            Assert.AreEqual(1, _world.Queued(CueSlot.UnderFire).Length, "another unit, after the player's interval");
        }

        [Test]
        public void ProducedUnit_PlaysProductionDoneAtTheProducer()
        {
            var prefab = _world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 90f), name: "Tank"));
            _world.SetBuildTime(prefab, 0.5f);
            var producer = Voiced(_world.MakeProducer(_world.SpawnBuilding(1, new float3(4f, 0f, 4f),
                new float2(4f, 4f), name: "Factory"), new float3(0f, 0f, -4f), prefab), CueSlot.ProductionDone);
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);

            _world.Produce(1, producer, prefab);
            var done = _world.QueuedOver(1f, CueSlot.ProductionDone);

            Assert.AreEqual(1, done.Count);
            Assert.AreEqual(_world.GetTypeId(producer), done[0].TypeId);
            Assert.AreEqual(new float3(4f, 0f, 4f), done[0].Position);
            Assert.IsTrue(CueRules.IsOwnerOnly(CueSlot.ProductionDone));
        }
    }
}
