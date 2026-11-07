using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Extra weapon mounts, salvos, muzzles in turn, spread and ammo, fired through the systems.</summary>
    public class WeaponTests : SimulationFixture
    {
        private const int Research = 4242;

        /// <summary>One yaw step.</summary>
        private const float YawTolerance = 2f * math.PI / HeadingByte.Steps;

        [Test]
        public void BothWeapons_FireAtOneTarget()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 100f);
            World.AddMount(gun, range: 6f, damage: 5f, cooldown: 100f);
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f));

            World.Run(1f);

            Assert.AreEqual(85f, World.GetHealth(enemy), "one shot from each weapon");
        }

        [Test]
        public void MinEffect_MountSkipsTargetsItsDamageBarelyHurts()
        {
            var missile = World.Own(ScriptableObject.CreateInstance<DamageType>());
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 100f);
            World.AddMount(gun, range: 6f, damage: 50f, cooldown: 100f, minEffect: 0.5f, damageType: missile);
            var soldier = World.SpawnUnit(2, new float3(4f, 0f, 0f));
            var writer = World.Writer(soldier);
            writer.AddBuffer(new[] { new ArmorModifier { DamageType = missile, Multiplier = 0.1f } });

            World.Run(1f);

            Assert.AreEqual(90f, World.GetHealth(soldier), "only the primary fired");
            Assert.AreEqual(Entity.Null, World.Mount(gun).Target);
        }

        [Test]
        public void IndependentAirMount_EngagesAndAimsAtAircraft_WhileThePrimaryShootsGround()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 100f,
                turreted: true);
            World.AddMount(gun, range: 6f, damage: 20f, cooldown: 100f, targets: WeaponTargets.Air,
                independent: true);
            var tank = World.SpawnUnit(2, new float3(-3f, 0f, 0f));
            var aircraft = World.SpawnAircraft(2, new float3(3f, 0f, 0f));

            World.Run(2f);

            Assert.AreEqual(tank, World.Get<AttackTarget>(gun).Value, "the primary keeps the ground target");
            Assert.AreEqual(90f, World.GetHealth(tank));
            Assert.AreEqual(80f, World.GetHealth(aircraft), "the mount picked the aircraft itself");
            var poses = World.Buffer<WeaponPose>(gun);
            Assert.AreEqual(-0.5f * math.PI, poses[0].YawRadians, YawTolerance, "the primary looks left");
            Assert.AreEqual(0f, poses[0].PitchRadians, 0.02f, "level with a ground target");
            Assert.AreEqual(0.5f * math.PI, poses[1].YawRadians, YawTolerance, "the mount looks right");
        }

        [Test]
        public void ResearchGatedMount_HoldsFireUntilTheResearchIsDone()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 100f);
            World.AddMount(gun, range: 6f, damage: 5f, cooldown: 0.5f, requiredResearchTypeId: Research);
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f), maxHealth: 1000f);

            World.Run(1f);
            Assert.AreEqual(990f, World.GetHealth(enemy), "locked mount stayed silent");

            World.EntityManager.GetBuffer<CompletedResearch>(World.Player(1))
                .Add(new CompletedResearch { TypeId = Research });
            World.Run(1f);

            Assert.IsTrue(World.Mount(gun).Unlocked);
            Assert.Less(World.GetHealth(enemy), 990f, "the unlocked mount opened fire");
        }

        [Test]
        public void ResearchGatedMount_Relocks_WhenTheResearchIsGone()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 0f, cooldown: 100f);
            World.AddMount(gun, range: 6f, damage: 10f, cooldown: 100f, requiredResearchTypeId: Research);
            World.EntityManager.GetBuffer<CompletedResearch>(World.Player(1))
                .Add(new CompletedResearch { TypeId = Research });
            World.Tick();
            Assert.IsTrue(World.Mount(gun).Unlocked);

            World.EntityManager.GetBuffer<CompletedResearch>(World.Player(1)).Clear();
            World.Tick();

            Assert.IsFalse(World.Mount(gun).Unlocked);
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
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 0f, cooldown: 100f);
            World.AddMount(gun, range: 6f, damage: 10f, cooldown: 0.2f, rounds: 2, reloadTime: 0.5f);
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f));

            World.Run(2f);
            Assert.AreEqual(80f, World.GetHealth(enemy), "two rounds, then it held fire");
            Assert.AreEqual(0, World.Mount(gun).Ammo.Current);

            World.EntityManager.DestroyEntity(enemy);
            World.EntityManager.AddComponent<Docked>(gun);
            World.Run(1.5f);

            Assert.AreEqual(2, World.Mount(gun).Ammo.Current, "reloaded while docked");
        }

        [Test]
        public void MagazineRefill_ReloadsAGroundUnitInTheField()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 0.5f);
            World.SetAmmo(gun, 2, 3f, AmmoRefill.Magazine);
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 4f));

            Assert.IsTrue(World.RunUntil(() => World.PrimaryAmmo(gun).Current == 0, 2f), "fires the magazine");
            Assert.AreEqual(80f, World.GetHealth(enemy));
            World.Run(2.5f);
            Assert.AreEqual(0, World.PrimaryAmmo(gun).Current, "a full reload time");
            Assert.IsTrue(World.RunUntil(() => World.GetHealth(enemy) <= 70f, 1.5f), "fires again once refilled");
        }

        [Test]
        public void Salvo_FiresEachShotAtTheInterval_ThenCoolsDown()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 5f);
            World.SetSalvo(gun, 3, 0.5f);
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 4f));

            Assert.IsTrue(World.RunUntil(() => World.GetHealth(enemy) < 100f, 1f), "the first shot");
            Assert.AreEqual(90f, World.GetHealth(enemy));
            World.Run(0.3f);
            Assert.AreEqual(90f, World.GetHealth(enemy), "waits the interval");
            World.Run(0.3f);
            Assert.AreEqual(80f, World.GetHealth(enemy), "the second shot");
            World.Run(0.5f);
            Assert.AreEqual(70f, World.GetHealth(enemy), "the third shot");
            World.Run(4.6f);
            Assert.AreEqual(70f, World.GetHealth(enemy), "the cooldown runs after the salvo");
            World.Run(0.6f);
            Assert.AreEqual(60f, World.GetHealth(enemy), "the next salvo");
        }

        [Test]
        public void SalvoOfOne_FiresOncePerCooldown()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 1f);
            World.SetSalvo(gun, 1, 0.1f);
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 4f));

            Assert.IsTrue(World.RunUntil(() => World.GetHealth(enemy) < 100f, 1f));
            World.Run(0.8f);
            Assert.AreEqual(90f, World.GetHealth(enemy), "the interval goes unused");
            World.Run(0.3f);
            Assert.AreEqual(80f, World.GetHealth(enemy));
        }

        [Test]
        public void Salvo_StopsWhenItsTargetDies()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 5f);
            World.SetSalvo(gun, 3, 0.5f);
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 4f));
            Assert.IsTrue(World.RunUntil(() => World.GetHealth(enemy) < 100f, 1f));

            World.Kill(enemy);
            World.Run(0.2f);

            var weapon = World.Primary(gun);
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
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, cooldown: 100f,
                projectile: World.ProjectilePrefab());
            World.SetSalvo(gun, 4, 0.2f);
            World.SetMuzzles(gun, new float3(-1f, 1f, 0f), new float3(0f, 1f, 0f), new float3(1f, 1f, 0f));
            var weapon = World.Primary(gun);
            weapon.ProjectileSpeed = 0.5f;
            World.SetPrimary(gun, weapon);
            World.SpawnUnit(2, new float3(0f, 0f, 4f));

            var starts = LaunchedFrom(1.5f);

            CollectionAssert.AreEqual(new[] { -1f, 0f, 1f, -1f }, starts, "the fourth shot wraps to the first muzzle");
        }

        [TestCase(Trajectory.Ballistic, true)]
        [TestCase(Trajectory.Rocket, false)]
        [TestCase(Trajectory.Direct, false)]
        public void Spread_ScattersEveryTrajectory_AndOnlyShellsKeepTheirLock(Trajectory trajectory, bool keepsLock)
        {
            var weapon = new Weapon { Spread = 4f, Aim = new WeaponAim { Course = ShotCourse.From(trajectory) } };
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
            var missile = new Weapon { Aim = new WeaponAim { Course = ShotCourse.Rocket } };

            Assert.AreEqual(new float3(1f, 2f, 3f), missile.Scatter(new float3(1f, 2f, 3f), 99u));
            Assert.IsTrue(missile.KeepsLock);
        }

        /// <summary>The x of each projectile's launch point, in the order they were fired.</summary>
        private List<float> LaunchedFrom(float seconds)
        {
            var seen = new HashSet<Entity>();
            var starts = new List<float>();
            using var query = World.Query(typeof(Projectile));
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                World.Tick();
                using var projectiles = query.ToEntityArray(Allocator.Temp);
                foreach (var projectile in projectiles)
                {
                    if (seen.Add(projectile))
                    {
                        starts.Add(math.round(World.Get<Projectile>(projectile).Start.x * 100f) / 100f);
                    }
                }
            }

            return starts;
        }
    }
}
