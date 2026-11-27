using System.Collections.Generic;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Destruction;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Destruction;

/// <summary>A sectioned building whose pieces play break clips dies once they have fallen, not as its last section breaks.</summary>
public class SectionDeathTests : SimulationFixture
{
    [Test]
    public void BuildingWithBreakClips_StaysWhileItsLastPiecesFall_ThenDies()
    {
        var clip = new SectionClip { StartRow = 1, FrameCount = 30, Fps = 30f };
        var hut = Hut();
        World.Add(hut, new SectionClips { BlownIn = clip, BlownOut = clip, Collapse = clip });
        World.Add(hut, new DeathStain { Radius = 2f });
        World.Tick();

        Shoot(hut);
        var deaths = World.QueuedOver(0.2f, CueSlot.Death).Count;
        Assert.AreEqual(1ul, World.Get<SectionedBuilding>(hut).Broken, "its one wall is down");
        Assert.IsTrue(World.EntityManager.Exists(hut), "but its pieces are still falling");
        Assert.IsTrue(World.EntityManager.IsComponentEnabled<Dying>(hut), "so nothing targets it");
        Assert.AreEqual(1, deaths, "and its death cue plays as the fall starts");

        Shoot(hut);
        deaths += World.QueuedOver(0.2f, CueSlot.Death).Count;
        Assert.IsTrue(World.EntityManager.Exists(hut), "a hit meanwhile does not bring the ruin early");

        deaths += World.QueuedOver(clip.Seconds, CueSlot.Death).Count;
        Assert.IsFalse(World.EntityManager.Exists(hut), "then it dies and leaves its ruin");
        Assert.AreEqual(1, deaths, "without a second cue");
    }

    // One anchored wall of 100 health.
    private Entity Hut()
    {
        var wall = new Section
        {
            Center = new float3(0f, 1.5f, 0f),
            HalfSize = new float3(2f, 1.5f, 0.15f),
            Health = 100f,
            MaxHealth = 100f,
            Anchored = true,
            Exterior = true,
            Outward = math.forward(),
        };
        var hut = World.SpawnBuilding(1, float3.zero, new float2(4f, 1f));
        SectionsSetup.Add(World.Writer(hut), [wall], [], default);
        var health = World.Get<Health>(hut);
        health.Max = health.Current = 100f;
        World.Set(hut, health);
        return hut;
    }

    private void Shoot(Entity hut) => World.QueueDamage(new DamageEvent
    {
        Target = hut,
        Origin = new float3(0f, 1.5f, 30f),
        Position = new float3(0f, 1.5f, 0f),
        SourceOwner = 2,
        Layers = WeaponTargets.Surface,
        Profile = new DamageProfile { Amount = 200f, EdgeFactor = 0.5f },
    });
}
