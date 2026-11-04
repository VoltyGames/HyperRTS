using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Cues.Voice;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace HyperRTS.Simulation.Tests.Cues
{
    /// <summary>Where and which fire cue a weapon plays.</summary>
    public class WeaponCueTests : CueFixture
    {
        [Test]
        public void Fire_PlaysAtTheMuzzle_FacingTheTarget()
        {
            var shooter = Rigged(World.AddWeapon(World.SpawnUnit(1, float3.zero, name: "Rifle")), CueSlot.Fire,
                new float3(0f, 1f, 2f));
            World.SpawnUnit(2, new float3(3f, 0f, 0f));

            var fire = Find(World.QueuedOver(0.5f), CueSlot.Fire);

            Assert.AreEqual(World.GetTypeId(shooter), fire.TypeId);
            Assert.AreEqual(1, fire.Faction);
            Assert.That(math.distance(new float3(2f, 1f, 0f), fire.Position), Is.LessThan(1e-3f),
                "the muzzle turns with the shooter");
            Assert.That(math.distance(new float3(1f, 0f, 0f), fire.Direction), Is.LessThan(1e-3f));
        }

        [Test]
        public void TurretFire_PlaysAtTheMuzzleTheTurretTurned()
        {
            var tank = World.AddWeapon(World.SpawnUnit(1, float3.zero, name: "Tank"), range: 10f,
                turreted: true);
            var weapon = World.Primary(tank);
            weapon.Aim.HasMuzzle = true;
            weapon.Aim.Muzzle = new float3(0f, 1f, 2f);
            World.SetPrimary(tank, weapon);
            Rigged(tank, CueSlot.Fire, weapon.Aim.Muzzle);
            World.SpawnUnit(2, new float3(8f, 0f, 0f), maxHealth: 1000f);

            var fire = Find(World.QueuedOver(1f), CueSlot.Fire);

            Assert.That(math.distance(new float3(2f, 1f, 0f), fire.Position), Is.LessThan(0.1f),
                "the hull stays put, its turret turns the muzzle onto the target");
        }

        [Test]
        public void EachWeapon_PlaysItsOwnFireCue_ByPoseIndex()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, cooldown: 100f);
            World.AddMount(gun, range: 6f, cooldown: 100f);
            var writer = World.Writer(gun);
            var sounds = EntitySoundsAuthoring.Add(writer);
            EntitySoundsAuthoring.Set(sounds, CueSlot.Fire, _cue);
            EntitySoundsAuthoring.Set(sounds, CueSlot.Fire, _cue, 1);
            World.SpawnUnit(2, new float3(4f, 0f, 0f));

            var fired = World.QueuedOver(1f, CueSlot.Fire).ConvertAll(cue => cue.Variant);

            CollectionAssert.AreEquivalent(new byte[] { 0, 1 }, fired);
        }

        [Test]
        public void ExtraWeaponWithoutAFireCue_FiresSilently()
        {
            var gun = Voiced(World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, cooldown: 100f),
                CueSlot.Fire);
            World.AddMount(gun, range: 6f, cooldown: 100f);
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f));

            var fired = World.QueuedOver(1f, CueSlot.Fire);

            Assert.Less(World.GetHealth(enemy), 75f, "both weapons fired");
            Assert.AreEqual(1, fired.Count, "the mount doesn't borrow the primary's cue");
            Assert.AreEqual(0, fired[0].Variant);
        }
    }
}
