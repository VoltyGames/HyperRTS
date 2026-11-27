using System.Collections.Generic;
using HyperRTS.Presentation.Audio;
using HyperRTS.Simulation.Cues.Announcements;
using HyperRTS.Simulation.Cues.Sounds;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Audio;

/// <summary>
/// One-shot sounds: the pool's instance limits, cooldown, priorities and loudness, the fade with distance, variations and the
/// announcer queue.
/// </summary>
public class SoundPlaybackTests
{
    private static readonly AnnouncementKind GameKind = AnnouncementKind.Game(0);

    private SoundCue _gun;
    private SoundCue _voice;
    private AnnouncerSet _set;
    private AudioClip _clip;

    [SetUp]
    public void SetUp()
    {
        _gun = Cue(2, 200);
        _voice = Cue(1, 10);
        _clip = AudioClip.Create("Test", 441, 1, 44100, false);
        _set = ScriptableObject.CreateInstance<AnnouncerSet>();
        _set.lines =
        [
            new() { kind = AnnouncementKind.UnitLost, cue = _gun, cooldown = 10f },
            new() { kind = AnnouncementKind.BaseUnderAttack, cue = _voice, cooldown = 30f },
            new() { kind = AnnouncementKind.LowPower, cooldown = 20f },
            new() { kind = GameKind, cue = _gun, cooldown = 5f },
        ];
        _set.maxWait = 4f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_set);
        Object.DestroyImmediate(_gun);
        Object.DestroyImmediate(_voice);
        Object.DestroyImmediate(_clip);
    }

    [Test]
    public void Cue_StopsAtItsInstanceLimit_UntilOneEnds()
    {
        var pool = new SoundPool(8);

        Assert.GreaterOrEqual(pool.Acquire(_gun, 1f, 0.0, 1.0), 0);
        Assert.GreaterOrEqual(pool.Acquire(_gun, 1f, 0.0, 2.0), 0);
        Assert.AreEqual(-1, pool.Acquire(_gun, 1f, 0.5, 1.0), "third copy skipped");
        Assert.GreaterOrEqual(pool.Acquire(_gun, 1f, 1.5, 1.0), 0, "the first one has ended");
        Assert.AreEqual(-1, pool.Acquire(_voice, SoundPool.Inaudible / 2f, 1.5, 1.0), "too faint to be heard");
    }

    [Test]
    public void Cue_WaitsOutItsCooldown_EvenAfterItEnds()
    {
        var pool = new SoundPool(8);
        _voice.cooldown = 8f;

        Assert.GreaterOrEqual(pool.Acquire(_voice, 1f, 0.0, 1.0), 0);
        Assert.AreEqual(-1, pool.Acquire(_voice, 1f, 5.0, 1.0), "still resting");
        Assert.GreaterOrEqual(pool.Acquire(_voice, 1f, 8.0, 1.0), 0);
    }

    [Test]
    public void FullPool_ImportantCueTakesTheWeakestVoice_NeverTheReverse()
    {
        var pool = new SoundPool(2);
        var first = pool.Acquire(_gun, 1f, 0.0, 5.0);
        pool.Acquire(_gun, 1f, 0.0, 5.0);

        Assert.AreEqual(first, pool.Acquire(_voice, 0.1f, 1.0, 1.0), "importance beats loudness");
        Assert.AreEqual(1, pool.Playing(_gun, 1.0));
        Assert.AreEqual(-1, pool.Acquire(_gun, 1f, 1.0, 1.0), "nothing less important to replace");
    }

    [Test]
    public void FullPool_EqualPriority_LouderSoundTakesTheQuietestVoice()
    {
        var shell = Cue(4, 200);
        var pool = new SoundPool(2);
        pool.Acquire(shell, 0.5f, 0.0, 5.0);
        var quiet = pool.Acquire(shell, 0.2f, 0.0, 5.0);

        Assert.AreEqual(-1, pool.Acquire(shell, 0.1f, 1.0, 1.0), "quieter than both");
        Assert.AreEqual(quiet, pool.Acquire(shell, 0.8f, 1.0, 1.0));
        Object.DestroyImmediate(shell);
    }

    [Test]
    public void Fade_IsFullWithinMin_FallsWithDistance_AndReachesSilenceAtMax()
    {
        Assert.AreEqual(1f, DistanceCurves.Volume(5f, 10f, 100f));
        Assert.AreEqual(0f, DistanceCurves.Volume(100f, 10f, 100f));
        var last = 1f;
        for (var distance = 11f; distance < 100f; distance += 3f)
        {
            var volume = DistanceCurves.Volume(distance, 10f, 100f);
            Assert.Less(volume, last, $"louder again at {distance} m");
            last = volume;
        }

        Assert.AreEqual(0.5f, DistanceCurves.Volume(20f, 10f, 100f), 0.03f, "near the minimum it halves per doubling");
        Assert.Less(DistanceCurves.CutoffShare(100f, 10f, 100f), DistanceCurves.CutoffShare(20f, 10f, 100f));
    }

    [Test]
    public void FarPlay_DrawsTheDistantVariations_NearPlayTheClips()
    {
        var near = AudioClip.Create("Near", 441, 1, 44100, false);
        var far = AudioClip.Create("Far", 441, 1, 44100, false);
        _gun.clips = [near];
        _gun.distantClips = [far];
        _gun.distantFrom = 40f;

        Assert.IsFalse(_gun.IsDistant(39f));
        Assert.IsTrue(_gun.IsDistant(40f));
        _gun.Pick(true, out var clip, out _);
        Assert.AreSame(far, clip);
        _gun.Pick(false, out clip, out _);
        Assert.AreSame(near, clip);

        _gun.spatial = false;
        Assert.IsFalse(_gun.IsDistant(100f), "2D sounds have no distance");
        Object.DestroyImmediate(near);
        Object.DestroyImmediate(far);
    }

    [Test]
    public void Alert_WaitsOutItsCooldown()
    {
        var queue = new AnnouncerQueue();

        Assert.IsTrue(queue.Offer(_set, AnnouncementKind.UnitLost, 0.0));
        Assert.IsFalse(queue.Offer(_set, AnnouncementKind.UnitLost, 5.0), "cooling down");
        Assert.IsTrue(queue.Offer(_set, AnnouncementKind.UnitLost, 10.0));
        Assert.IsFalse(queue.Offer(_set, AnnouncementKind.LowPower, 0.0), "a line without a cue is silent");
        Assert.IsFalse(queue.Offer(_set, AnnouncementKind.UnitReady, 0.0), "a kind without a line is silent");
    }

    [Test]
    public void GameKind_SpeaksItsOwnLine_OnItsOwnCooldown()
    {
        var queue = new AnnouncerQueue();
        queue.Offer(_set, AnnouncementKind.UnitLost, 0.0);

        Assert.IsTrue(queue.Offer(_set, GameKind, 0.0), "cools apart from the built-in kinds");
        Assert.IsFalse(queue.Offer(_set, GameKind, 4.0));
        Assert.IsTrue(queue.Offer(_set, GameKind, 5.0));
    }

    [Test]
    public void MostImportantLine_SpeaksFirst_AndStaleLinesDrop()
    {
        var queue = new AnnouncerQueue();
        queue.Offer(_set, AnnouncementKind.UnitLost, 0.0);
        queue.Offer(_set, AnnouncementKind.BaseUnderAttack, 0.5);

        Assert.AreSame(_voice, queue.Next(1.0, _set.maxWait));
        Assert.AreSame(_gun, queue.Next(1.0, _set.maxWait));
        Assert.IsNull(queue.Next(1.0, _set.maxWait));

        queue.Offer(_set, AnnouncementKind.UnitLost, 20.0);
        Assert.IsNull(queue.Next(20.0 + _set.maxWait + 1.0, _set.maxWait));
        Assert.AreEqual(0, queue.Count);
    }

    [Test]
    public void Variations_NeverRepeatBackToBack_AndAllPlay()
    {
        var clips = new[]
        {
            AudioClip.Create("A", 441, 1, 44100, false), AudioClip.Create("B", 441, 1, 44100, false),
            AudioClip.Create("C", 441, 1, 44100, false),
        };
        _gun.clips = clips;
        var played = new HashSet<AudioClip>();
        AudioClip last = null;
        for (var i = 0; i < 300; i++)
        {
            _gun.Pick(false, out var clip, out _);
            Assert.AreNotSame(last, clip, $"draw {i} repeats the last clip");
            played.Add(clip);
            last = clip;
        }

        Assert.AreEqual(clips.Length, played.Count, "every variation plays");
        foreach (var clip in clips)
        {
            Object.DestroyImmediate(clip);
        }
    }

    private static SoundCue Cue(int maxInstances, int priority)
    {
        var cue = ScriptableObject.CreateInstance<SoundCue>();
        cue.maxInstances = maxInstances;
        cue.priority = priority;
        return cue;
    }
}
