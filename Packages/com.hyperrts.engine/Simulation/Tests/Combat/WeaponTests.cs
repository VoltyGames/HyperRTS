using System.Collections.Generic;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Extra weapon mounts, salvos, muzzles in turn, spread and ammo, fired through the systems.</summary>
    public class WeaponTests
    {
        private const int Research = 4242;

        /// <summary>One yaw step.</summary>
        private const float YawTolerance = 2f * math.PI / HeadingByte.Steps;

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
        public void IndependentAirMount_EngagesAndAimsAtAircraft_WhileThePrimaryShootsGround()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 100f,
                turreted: true);
            _world.AddMount(gun, range: 6f, damage: 20f, cooldown: 100f, targets: WeaponTargets.Air,
                independent: true);
            var tank = _world.SpawnUnit(2, new float3(-3f, 0f, 0f));
            var aircraft = _world.SpawnAircraft(2, new float3(3f, 0f, 0f));

            _world.Run(2f);

            Assert.AreEqual(tank, _world.Get<AttackTarget>(gun).Value, "the primary keeps the ground target");
            Assert.AreEqual(90f, _world.GetHealth(tank));
            Assert.AreEqual(80f, _world.GetHealth(aircraft), "the mount picked the aircraft itself");
            var poses = _world.EntityManager.GetBuffer<WeaponPose>(gun);
            Assert.AreEqual(-0.5f * math.PI, poses[0].YawRadians, YawTolerance, "the primary looks left");
            Assert.AreEqual(0f, poses[0].PitchRadians, 0.02f, "level with a ground target");
            Assert.AreEqual(0.5f * math.PI, poses[1].YawRadians, YawTolerance, "the mount looks right");
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
        public void MagazineRefill_ReloadsEveryRoundAtOnce_OnlyOnceEmpty()
        {
            var ammo = new Ammo { Max = 3, Current = 1, ReloadTime = 2f, Refill = AmmoRefill.Magazine };

            ammo.Reload(5f);
            Assert.AreEqual(1, ammo.Current, "a part-empty magazine waits");
            ammo.Current = 0;
            ammo.Reload(1.5f);
            Assert.AreEqual(0, ammo.Current);
            ammo.Reload(0.6f);
            Assert.AreEqual(3, ammo.Current);
            Assert.AreEqual(0f, ammo.ReloadElapsed);
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

        [Test]
        public void MagazineRefill_ReloadsAGroundUnitInTheField()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 0.5f);
            var writer = new EntityManagerWriter(_world.EntityManager, gun);
            WeaponSetup.AddAmmo(ref writer, 2, 3f, AmmoRefill.Magazine);
            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 4f));

            Assert.IsTrue(_world.RunUntil(() => _world.Get<Ammo>(gun).Current == 0, 2f), "fires the magazine");
            Assert.AreEqual(80f, _world.GetHealth(enemy));
            _world.Run(2.5f);
            Assert.AreEqual(0, _world.Get<Ammo>(gun).Current, "a full reload time");
            Assert.IsTrue(_world.RunUntil(() => _world.GetHealth(enemy) <= 70f, 1.5f), "fires again once refilled");
        }

        [Test]
        public void Salvo_FiresEachShotAtTheInterval_ThenCoolsDown()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 5f);
            _world.SetSalvo(gun, 3, 0.5f);
            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 4f));

            Assert.IsTrue(_world.RunUntil(() => _world.GetHealth(enemy) < 100f, 1f), "the first shot");
            Assert.AreEqual(90f, _world.GetHealth(enemy));
            _world.Run(0.3f);
            Assert.AreEqual(90f, _world.GetHealth(enemy), "waits the interval");
            _world.Run(0.3f);
            Assert.AreEqual(80f, _world.GetHealth(enemy), "the second shot");
            _world.Run(0.5f);
            Assert.AreEqual(70f, _world.GetHealth(enemy), "the third shot");
            _world.Run(4.6f);
            Assert.AreEqual(70f, _world.GetHealth(enemy), "the cooldown runs after the salvo");
            _world.Run(0.6f);
            Assert.AreEqual(60f, _world.GetHealth(enemy), "the next salvo");
        }

        [Test]
        public void SalvoOfOne_FiresOncePerCooldown()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 1f);
            _world.SetSalvo(gun, 1, 0.1f);
            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 4f));

            Assert.IsTrue(_world.RunUntil(() => _world.GetHealth(enemy) < 100f, 1f));
            _world.Run(0.8f);
            Assert.AreEqual(90f, _world.GetHealth(enemy), "the interval goes unused");
            _world.Run(0.3f);
            Assert.AreEqual(80f, _world.GetHealth(enemy));
        }

        [Test]
        public void Salvo_StopsWhenItsTargetDies()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 5f);
            _world.SetSalvo(gun, 3, 0.5f);
            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 4f));
            Assert.IsTrue(_world.RunUntil(() => _world.GetHealth(enemy) < 100f, 1f));

            _world.Kill(enemy);
            _world.Run(0.2f);

            var weapon = _world.Get<Weapon>(gun);
            Assert.AreEqual(0, weapon.SalvoShot);
            Assert.Greater(weapon.CooldownRemaining, 0.5f, $"the full cooldown runs, not the interval: {weapon.CooldownRemaining}");
        }

        [Test]
        public void EndSalvo_CountsTheCooldownFromTheLastShot()
        {
            var weapon = new Weapon { Cooldown = 5f, SalvoSize = 3, SalvoInterval = 0.5f };
            weapon.StartCooldown();
            weapon.Tick(0.2f);

            weapon.EndSalvo();

            Assert.AreEqual(0, weapon.SalvoShot);
            Assert.AreEqual(4.8f, weapon.CooldownRemaining, 1e-4f);
        }

        [Test]
        public void Salvo_FiresFromEachMuzzleInTurn()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, cooldown: 100f,
                projectile: _world.ProjectilePrefab());
            _world.SetSalvo(gun, 4, 0.2f);
            _world.SetMuzzles(gun, new float3(-1f, 1f, 0f), new float3(0f, 1f, 0f), new float3(1f, 1f, 0f));
            var weapon = _world.Get<Weapon>(gun);
            weapon.ProjectileSpeed = 0.5f;
            _world.EntityManager.SetComponentData(gun, weapon);
            _world.SpawnUnit(2, new float3(0f, 0f, 4f));

            var starts = LaunchedFrom(1.5f);

            CollectionAssert.AreEqual(new[] { -1f, 0f, 1f, -1f }, starts, "the fourth shot wraps to the first muzzle");
        }

        [TestCase(Trajectory.Ballistic, true)]
        [TestCase(Trajectory.Rocket, false)]
        [TestCase(Trajectory.Direct, false)]
        public void Spread_ScattersEveryTrajectory_AndOnlyShellsKeepTheirLock(Trajectory trajectory, bool keepsLock)
        {
            var weapon = new Weapon { Spread = 4f, Trajectory = trajectory };
            var aim = new float3(10f, 0f, 20f);

            var landed = new HashSet<float3>();
            for (var shot = 0u; shot < 6; shot++)
            {
                var point = weapon.Scatter(aim, math.hash(new uint2(shot, 7u)));
                Assert.LessOrEqual(math.distance(point.xz, aim.xz), 4f + 1e-4f);
                landed.Add(point);
            }

            Assert.Greater(landed.Count, 1, "a ripple spreads out");
            Assert.AreEqual(keepsLock, weapon.KeepsLock);
        }

        [Test]
        public void NoSpread_KeepsTheAimAndTheLock()
        {
            var missile = new Weapon { Trajectory = Trajectory.Rocket };

            Assert.AreEqual(new float3(1f, 2f, 3f), missile.Scatter(new float3(1f, 2f, 3f), 99u));
            Assert.IsTrue(missile.KeepsLock);
        }

        /// <summary>The x of each projectile's launch point, in the order they were fired.</summary>
        private List<float> LaunchedFrom(float seconds)
        {
            var seen = new HashSet<Entity>();
            var starts = new List<float>();
            using var query = _world.EntityManager.CreateEntityQuery(typeof(Projectile));
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                _world.Tick();
                using var projectiles = query.ToEntityArray(Allocator.Temp);
                foreach (var projectile in projectiles)
                {
                    if (seen.Add(projectile))
                    {
                        starts.Add(math.round(_world.Get<Projectile>(projectile).Start.x * 100f) / 100f);
                    }
                }
            }

            return starts;
        }
    }
}
