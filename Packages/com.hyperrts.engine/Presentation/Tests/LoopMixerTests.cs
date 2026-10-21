using HyperRTS.Presentation.Audio;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Which loops win the sources: in range, nearest per cue instance limit, kept on their source.</summary>
    public class LoopMixerTests
    {
        private SoundCue _engine;
        private SoundCue _hum;
        private AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            _clip = AudioClip.Create("Loop", 100, 1, 1000, false);
            _engine = Cue(2, 50f);
            _hum = Cue(4, 20f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_engine);
            Object.DestroyImmediate(_hum);
            Object.DestroyImmediate(_clip);
        }

        private SoundCue Cue(int maxInstances, float maxDistance)
        {
            var cue = ScriptableObject.CreateInstance<SoundCue>();
            cue.clips = new[] { _clip };
            cue.maxInstances = maxInstances;
            cue.maxDistance = maxDistance;
            return cue;
        }

        private static LoopCandidate Loop(int entity, SoundCue cue, float distance, float level = 1f) => new()
        {
            Entity = new Entity { Index = entity, Version = 1 }, Slot = LoopSlot.EngineIdle, Cue = cue,
            Level = level, PitchScale = 1f, DistanceSq = distance * distance,
        };

        [Test]
        public void ManyOfOneCue_OnlyTheNearestWithinItsLimitPlay()
        {
            var mixer = new LoopMixer(8);
            mixer.Begin();
            mixer.Offer(Loop(1, _engine, 30f));
            mixer.Offer(Loop(2, _engine, 10f));
            mixer.Offer(Loop(3, _engine, 20f));
            mixer.Resolve();

            Assert.AreEqual(2, mixer.Winners.Count);
            Assert.AreEqual(2, mixer.Winners[0].Loop.Entity.Index);
            Assert.AreEqual(3, mixer.Winners[1].Loop.Entity.Index);
        }

        [Test]
        public void OutOfRange_AndSilentLoops_DontPlay()
        {
            var mixer = new LoopMixer(8);
            mixer.Begin();
            mixer.Offer(Loop(1, _hum, 25f));
            mixer.Offer(Loop(2, _hum, 5f, level: 0f));
            mixer.Resolve();

            Assert.IsEmpty(mixer.Winners);
        }

        [Test]
        public void FullMixer_KeepsTheNearestOverall()
        {
            var mixer = new LoopMixer(2);
            mixer.Begin();
            mixer.Offer(Loop(1, _hum, 15f));
            mixer.Offer(Loop(2, _engine, 5f));
            mixer.Offer(Loop(3, _hum, 1f));
            mixer.Resolve();

            CollectionAssert.AreEquivalent(new[] { 3, 2 },
                new[] { mixer.Winners[0].Loop.Entity.Index, mixer.Winners[1].Loop.Entity.Index });
        }

        [Test]
        public void PlayingLoop_KeepsItsSource_AndALoserFreesIt()
        {
            var mixer = new LoopMixer(4);
            mixer.Begin();
            mixer.Offer(Loop(1, _engine, 10f));
            mixer.Resolve();
            var source = mixer.Winners[0].Source;
            Assert.IsTrue(mixer.Winners[0].Claimed);

            mixer.Begin();
            mixer.Offer(Loop(2, _engine, 40f));
            mixer.Offer(Loop(1, _engine, 12f));
            mixer.Resolve();
            Assert.AreEqual(source, mixer.Winners[0].Source);
            Assert.IsFalse(mixer.Winners[0].Claimed, "a kept loop doesn't restart");
            Assert.IsTrue(mixer.Winners[1].Claimed);
            Assert.AreNotEqual(source, mixer.Winners[1].Source);

            mixer.Begin();
            mixer.Offer(Loop(2, _engine, 40f));
            mixer.Resolve();
            CollectionAssert.AreEqual(new[] { source }, mixer.Released);
            Assert.IsFalse(mixer.Winners[0].Claimed);
        }
    }
}
