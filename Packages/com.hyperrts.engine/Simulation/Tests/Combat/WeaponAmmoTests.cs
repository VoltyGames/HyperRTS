using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Weapon ammo: one round per reload time, magazines, mount rounds, refilling docked and in the field.</summary>
    public class WeaponAmmoTests : SimulationFixture
    {
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
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), new TestWeapon
            {
                Range = 6f,
                Damage = 0f,
                Cooldown = 100f,
            });
            World.AddMount(gun, new TestMount
            {
                Range = 6f,
                Damage = 10f,
                Cooldown = 0.2f,
                Rounds = 2,
                ReloadTime = 0.5f,
            });
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
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), new TestWeapon
            {
                Range = 6f,
                Damage = 10f,
                Cooldown = 0.5f,
            });
            World.SetAmmo(gun, 2, 3f, AmmoRefill.Magazine);
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 4f));

            Assert.IsTrue(World.RunUntil(() => World.PrimaryAmmo(gun).Current == 0, 2f), "fires the magazine");
            Assert.AreEqual(80f, World.GetHealth(enemy));
            World.Run(2.5f);
            Assert.AreEqual(0, World.PrimaryAmmo(gun).Current, "a full reload time");
            Assert.IsTrue(World.RunUntil(() => World.GetHealth(enemy) <= 70f, 1.5f), "fires again once refilled");
        }
    }
}
