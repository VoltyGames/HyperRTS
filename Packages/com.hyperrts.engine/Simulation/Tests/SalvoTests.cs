using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Salvos, muzzles fired in turn and whole-magazine reloads.</summary>
    public class SalvoTests
    {
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
        public void Salvo_FiresFromEachMuzzleInTurn()
        {
            var shooter = _world.SpawnUnit(1, float3.zero);
            var gun = _world.AddWeapon(shooter, range: 6f, cooldown: 100f, projectile: _world.ProjectilePrefab());
            _world.SetSalvo(gun, 4, 0.2f);
            _world.SetMuzzles(gun, new float3(-1f, 1f, 0f), new float3(0f, 1f, 0f), new float3(1f, 1f, 0f));
            var weapon = _world.Get<Weapon>(gun);
            weapon.ProjectileSpeed = 0.5f;
            _world.EntityManager.SetComponentData(gun, weapon);
            _world.SpawnUnit(2, new float3(0f, 0f, 4f));

            var starts = LaunchedFrom(1.5f);

            CollectionAssert.AreEqual(new[] { -1f, 0f, 1f, -1f }, starts, "the fourth shot wraps to the first muzzle");
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
