using System.Collections.Generic;
using HyperRTS.Presentation.Audio;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The announcer queue's cooldowns, priority order and stale lines.</summary>
    public class AnnouncerQueueTests
    {
        private AnnouncerSet _set;
        private SoundCue _lost;
        private SoundCue _attack;

        private static readonly AnnouncementKind GameKind = AnnouncementKind.Game(0);

        [SetUp]
        public void SetUp()
        {
            _lost = ScriptableObject.CreateInstance<SoundCue>();
            _lost.priority = 100;
            _attack = ScriptableObject.CreateInstance<SoundCue>();
            _attack.priority = 10;
            _set = ScriptableObject.CreateInstance<AnnouncerSet>();
            _set.lines = new List<AnnouncerSet.Line>
            {
                new() { kind = AnnouncementKind.UnitLost, cue = _lost, cooldown = 10f },
                new() { kind = AnnouncementKind.BaseUnderAttack, cue = _attack, cooldown = 30f },
                new() { kind = AnnouncementKind.LowPower, cooldown = 20f },
                new() { kind = GameKind, cue = _lost, cooldown = 5f },
            };
            _set.maxWait = 4f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_set);
            Object.DestroyImmediate(_lost);
            Object.DestroyImmediate(_attack);
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
        public void MostImportantLine_SpeaksFirst()
        {
            var queue = new AnnouncerQueue();
            queue.Offer(_set, AnnouncementKind.UnitLost, 0.0);
            queue.Offer(_set, AnnouncementKind.BaseUnderAttack, 0.5);

            Assert.AreSame(_attack, queue.Next(1.0, _set.maxWait));
            Assert.AreSame(_lost, queue.Next(1.0, _set.maxWait));
            Assert.IsNull(queue.Next(1.0, _set.maxWait));
        }

        [Test]
        public void StaleLines_AreDropped()
        {
            var queue = new AnnouncerQueue();
            queue.Offer(_set, AnnouncementKind.UnitLost, 0.0);

            Assert.IsNull(queue.Next(_set.maxWait + 1.0, _set.maxWait));
            Assert.AreEqual(0, queue.Count);
        }
    }
}
