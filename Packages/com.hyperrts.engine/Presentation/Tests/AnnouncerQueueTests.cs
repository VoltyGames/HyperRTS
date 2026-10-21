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

        [SetUp]
        public void SetUp()
        {
            _lost = ScriptableObject.CreateInstance<SoundCue>();
            _lost.priority = 100;
            _attack = ScriptableObject.CreateInstance<SoundCue>();
            _attack.priority = 10;
            _set = ScriptableObject.CreateInstance<AnnouncerSet>();
            _set.unitLost = new AnnouncerSet.Line { cue = _lost, cooldown = 10f };
            _set.baseUnderAttack = new AnnouncerSet.Line { cue = _attack, cooldown = 30f };
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

            Assert.IsTrue(queue.Offer(_set, CueSlot.AnnounceUnitLost, 0.0));
            Assert.IsFalse(queue.Offer(_set, CueSlot.AnnounceUnitLost, 5.0), "cooling down");
            Assert.IsTrue(queue.Offer(_set, CueSlot.AnnounceUnitLost, 10.0));
            Assert.IsFalse(queue.Offer(_set, CueSlot.AnnounceLowPower, 0.0), "a line without a cue is silent");
        }

        [Test]
        public void MostImportantLine_SpeaksFirst()
        {
            var queue = new AnnouncerQueue();
            queue.Offer(_set, CueSlot.AnnounceUnitLost, 0.0);
            queue.Offer(_set, CueSlot.AnnounceBaseUnderAttack, 0.5);

            Assert.AreSame(_attack, queue.Next(1.0, _set.maxWait));
            Assert.AreSame(_lost, queue.Next(1.0, _set.maxWait));
            Assert.IsNull(queue.Next(1.0, _set.maxWait));
        }

        [Test]
        public void StaleLines_AreDropped()
        {
            var queue = new AnnouncerQueue();
            queue.Offer(_set, CueSlot.AnnounceUnitLost, 0.0);

            Assert.IsNull(queue.Next(_set.maxWait + 1.0, _set.maxWait));
            Assert.AreEqual(0, queue.Count);
        }
    }
}
