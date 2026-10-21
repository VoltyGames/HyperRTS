using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Extra weapons beside the primary: shared and own targets, armor thresholds, stats, research and ammo.</summary>
    public class WeaponMountTests
    {
        private const int Research = 4242;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void BothWeapons_FireAtOneTarget()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 100f);
            _world.AddMount(gun, range: 6f, damage: 5f, cooldown: 100f);
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f));

            _world.Run(1f);

            Assert.AreEqual(85f, _world.GetHealth(enemy), "one shot from each weapon");
        }

        [Test]
        public void MinEffect_MountSkipsTargetsItsDamageBarelyHurts()
        {
            var missile = _world.Own(ScriptableObject.CreateInstance<DamageType>());
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 100f);
            _world.AddMount(gun, range: 6f, damage: 50f, cooldown: 100f, minEffect: 0.5f, damageType: missile);
            var soldier = _world.SpawnUnit(2, new float3(4f, 0f, 0f));
            var writer = new EntityManagerWriter(_world.EntityManager, soldier);
            writer.AddBuffer(new[] { new ArmorModifier { DamageType = missile, Multiplier = 0.1f } });

            _world.Run(1f);

            Assert.AreEqual(90f, _world.GetHealth(soldier), "only the primary fired");
            Assert.AreEqual(Entity.Null, _world.Mount(gun).Target);
        }

        [Test]
        public void IndependentAirMount_EngagesAircraft_WhileThePrimaryShootsGround()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 100f);
            _world.AddMount(gun, range: 6f, damage: 20f, cooldown: 100f, targets: WeaponTargets.Air,
                independent: true);
            var tank = _world.SpawnUnit(2, new float3(-3f, 0f, 0f));
            var aircraft = _world.SpawnAircraft(2, new float3(3f, 0f, 0f));

            _world.Run(2f);

            Assert.AreEqual(tank, _world.Get<AttackTarget>(gun).Value, "the primary keeps the ground target");
            Assert.AreEqual(90f, _world.GetHealth(tank));
            Assert.AreEqual(80f, _world.GetHealth(aircraft), "the mount picked the aircraft itself");
        }

        [Test]
        public void DamageModifier_ReachesMounts()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), Stance.Passive);
            _world.AddMount(gun, range: 5f, damage: 40f, cooldown: 2f);
            _world.EntityManager.GetBuffer<StatModifier>(gun).Add(new StatModifier
            {
                Stat = Stat.Damage, Percent = 0.25f, Source = StatSource.Upgrade(7),
            });

            _world.Tick();

            var mount = _world.Mount(gun);
            Assert.AreEqual(50f, mount.Weapon.Damage, 1e-4f);
            Assert.AreEqual(5f, mount.Weapon.Range, 1e-4f);
            Assert.AreEqual(2f, mount.Weapon.Cooldown, 1e-4f);
        }

        [Test]
        public void ResearchGatedMount_HoldsFireUntilTheResearchIsDone()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 100f);
            _world.AddMount(gun, range: 6f, damage: 5f, cooldown: 0.5f, requiredResearchTypeId: Research);
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f), maxHealth: 1000f);

            _world.Run(1f);
            Assert.AreEqual(990f, _world.GetHealth(enemy), "locked mount stayed silent");

            _world.EntityManager.GetBuffer<CompletedResearch>(_world.Player(1))
                .Add(new CompletedResearch { TypeId = Research });
            _world.Run(1f);

            Assert.IsTrue(_world.Mount(gun).Unlocked);
            Assert.Less(_world.GetHealth(enemy), 990f, "the unlocked mount opened fire");
        }

        [Test]
        public void ResearchGatedMount_Relocks_WhenTheResearchIsGone()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 0f, cooldown: 100f);
            _world.AddMount(gun, range: 6f, damage: 10f, cooldown: 100f, requiredResearchTypeId: Research);
            _world.EntityManager.GetBuffer<CompletedResearch>(_world.Player(1))
                .Add(new CompletedResearch { TypeId = Research });
            _world.Tick();
            Assert.IsTrue(_world.Mount(gun).Unlocked);

            _world.EntityManager.GetBuffer<CompletedResearch>(_world.Player(1)).Clear();
            _world.Tick();

            Assert.IsFalse(_world.Mount(gun).Unlocked);
        }

        [Test]
        public void Ammo_RefillsOneRoundPerReloadTime_AndRestsWhenFull()
        {
            var ammo = new Ammo { Max = 2, Current = 0, ReloadTime = 0.5f };

            ammo.Reload(0.4f);
            Assert.AreEqual(0, ammo.Current);
            ammo.Reload(0.2f);
            Assert.AreEqual(1, ammo.Current);
            Assert.AreEqual(0.1f, ammo.ReloadElapsed, 1e-5f, "the overflow carries into the next round");

            ammo.Current = 2;
            ammo.Reload(0.3f);
            Assert.AreEqual(0f, ammo.ReloadElapsed, "a full weapon doesn't bank reload time");
        }

        [Test]
        public void WeaponCooldown_TicksToZero_AndRestartsAtTheFullCooldown()
        {
            var weapon = new Weapon { Cooldown = 1f };
            Assert.IsFalse(weapon.CoolingDown);

            weapon.StartCooldown();
            weapon.Tick(0.4f);
            Assert.IsTrue(weapon.CoolingDown);
            Assert.AreEqual(0.6f, weapon.CooldownRemaining, 1e-5f);

            weapon.Tick(5f);
            Assert.IsFalse(weapon.CoolingDown);
            Assert.AreEqual(0f, weapon.CooldownRemaining, "never goes below zero");
        }

        [Test]
        public void MountRounds_RunOut_AndRefillWhileDocked()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 0f, cooldown: 100f);
            _world.AddMount(gun, range: 6f, damage: 10f, cooldown: 0.2f, rounds: 2, reloadTime: 0.5f);
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f));

            _world.Run(2f);
            Assert.AreEqual(80f, _world.GetHealth(enemy), "two rounds, then it held fire");
            Assert.AreEqual(0, _world.Mount(gun).Rounds);

            _world.EntityManager.DestroyEntity(enemy);
            _world.EntityManager.AddComponent<Docked>(gun);
            _world.Run(1.5f);

            Assert.AreEqual(2, _world.Mount(gun).Rounds, "reloaded while docked");
        }
    }
}
