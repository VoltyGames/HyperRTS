using System.Collections.Generic;
using HyperRTS.Presentation.Audio;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Audio
{
    /// <summary>One-shot sounds: the pool's instance limits and priorities, the voice bank and the announcer queue.</summary>
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
            _set.lines = new List<AnnouncerSet.Line>
            {
                new() { kind = AnnouncementKind.UnitLost, cue = _gun, cooldown = 10f },
                new() { kind = AnnouncementKind.BaseUnderAttack, cue = _voice, cooldown = 30f },
                new() { kind = AnnouncementKind.LowPower, cooldown = 20f },
                new() { kind = GameKind, cue = _gun, cooldown = 5f },
            };
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

            Assert.GreaterOrEqual(pool.Acquire(_gun, 0.0, 1.0), 0);
            Assert.GreaterOrEqual(pool.Acquire(_gun, 0.0, 2.0), 0);
            Assert.AreEqual(-1, pool.Acquire(_gun, 0.5, 1.0), "third copy skipped");
            Assert.GreaterOrEqual(pool.Acquire(_gun, 1.5, 1.0), 0, "the first one has ended");
        }

        [Test]
        public void FullPool_ImportantCueTakesTheWeakestVoice_NeverTheReverse()
        {
            var pool = new SoundPool(2);
            var first = pool.Acquire(_gun, 0.0, 5.0);
            pool.Acquire(_gun, 0.0, 5.0);

            Assert.AreEqual(first, pool.Acquire(_voice, 1.0, 1.0));
            Assert.AreEqual(1, pool.Playing(_gun, 1.0));
            Assert.AreEqual(-1, pool.Acquire(_gun, 1.0, 1.0), "nothing less important to replace");
        }

        [Test]
        public void VoiceBank_LoadsTheVoiceFromTheCue_AndDisposeRemovesThem()
        {
            _voice.volume = new Vector2(0.4f, 0.4f);
            _voice.spatial = true;
            _voice.minDistance = 3f;
            _voice.maxDistance = 90f;
            var bank = new VoiceBank("Test Voices", 2, loop: true);

            var source = bank.Load(1, _voice, _clip, 1.2f);

            Assert.AreSame(_clip, source.clip);
            Assert.AreEqual(1.2f, source.pitch, 1e-5f);
            Assert.AreEqual(0.4f, source.volume, 1e-5f);
            Assert.AreEqual(10, source.priority);
            Assert.AreEqual(1f, source.spatialBlend);
            Assert.AreEqual((3f, 90f), (source.minDistance, source.maxDistance));
            Assert.IsTrue(source.loop);
            Assert.AreSame(source, bank[1]);

            bank.Dispose();
            Assert.IsTrue(source == null);
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

        private static SoundCue Cue(int maxInstances, int priority)
        {
            var cue = ScriptableObject.CreateInstance<SoundCue>();
            cue.maxInstances = maxInstances;
            cue.priority = priority;
            return cue;
        }
    }
}
