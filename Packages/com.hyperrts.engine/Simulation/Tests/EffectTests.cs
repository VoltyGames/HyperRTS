using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Effect events from weapons and deaths, and who sees them, run through the systems.</summary>
    public class EffectTests
    {
        private TestWorld _world;
        private GameObject _rig;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _rig = new GameObject("Rig");
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_rig);
        }

        private Entity Rigged(Entity entity, CueSlot slot, float3 socket)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            EffectSetup.Set(EffectSetup.Add(ref writer), slot, _rig, socket);
            return entity;
        }

        [Test]
        public void Fire_PlaysAtTheMuzzle_FacingTheTarget()
        {
            var shooter = Rigged(_world.AddWeapon(_world.SpawnUnit(1, float3.zero, name: "Rifle")), CueSlot.Fire,
                new float3(0f, 1f, 2f));
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            var fire = _world.QueuedOver(0.5f).Find(effect => effect.Slot == CueSlot.Fire);

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
            var weapon = _world.Get<Combat.Weapon>(tank);
            weapon.HasMuzzle = true;
            weapon.Muzzle = new float3(0f, 1f, 2f);
            _world.EntityManager.SetComponentData(tank, weapon);
            Rigged(tank, CueSlot.Fire, weapon.Muzzle);
            _world.SpawnUnit(2, new float3(8f, 0f, 0f), maxHealth: 1000f);

            var fire = _world.QueuedOver(1f).Find(effect => effect.Slot == CueSlot.Fire);

            Assert.That(math.distance(new float3(2f, 1f, 0f), fire.Position), Is.LessThan(0.1f),
                "the hull stays put, its turret turns the muzzle onto the target");
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
        public void Death_PlaysTheVictimsDeathRig()
        {
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero), damage: 100f);
            var victim = Rigged(_world.SpawnUnit(2, new float3(3f, 0f, 0f), name: "Victim"), CueSlot.Death,
                float3.zero);
            var typeId = _world.GetTypeId(victim);

            var death = _world.QueuedOver(1f).Find(effect => effect.Slot == CueSlot.Death);

            Assert.AreEqual(typeId, death.TypeId);
            Assert.AreEqual(2, death.Faction);
            Assert.AreEqual(new float3(3f, 0f, 0f), death.Position);
        }

        [Test]
        public void Effects_ReachWhoeverSeesThem_StealthOnlyDetectors()
        {
            _world.SpawnUnit(2, new float3(50f, 0f, 50f));
            _world.Tick();
            _world.EntityManager.CompleteAllTrackedJobs();
            var fog = Single<FogOfWar>();
            var relations = Single<FactionRelations>();
            var hidden = new CueEvent { Slot = CueSlot.Fire, Faction = 1 };
            var seen = new CueEvent { Slot = CueSlot.Fire, Faction = 1, Position = new float3(52f, 0f, 50f) };

            Assert.IsFalse(CueRules.IsVisible(hidden, 2, fog, relations), "hidden by fog");
            Assert.IsTrue(CueRules.IsVisible(seen, 2, fog, relations));
            Assert.IsTrue(CueRules.IsVisible(hidden, 1, fog, relations), "own effects always");
            seen.Stealthed = true;
            Assert.IsFalse(CueRules.IsVisible(seen, 2, fog, relations), "undetected stealth shows nothing");
            var inactive = new LocalFogView { Viewer = 2, Active = false };
            Assert.IsTrue(CueRules.IsSeenLocally(hidden, inactive, fog, relations), "no local fog shows all");
        }

        private T Single<T>() where T : unmanaged, IComponentData
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(T));
            return query.GetSingleton<T>();
        }
    }
}
