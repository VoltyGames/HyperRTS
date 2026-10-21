using HyperRTS.Presentation.Audio;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public class VoiceBankTests
    {
        private SoundCue _cue;
        private AudioClip _clip;
        private VoiceBank _bank;

        [SetUp]
        public void SetUp()
        {
            _cue = ScriptableObject.CreateInstance<SoundCue>();
            _cue.volume = new Vector2(0.4f, 0.4f);
            _cue.priority = 42;
            _cue.spatial = true;
            _cue.minDistance = 3f;
            _cue.maxDistance = 90f;
            _clip = AudioClip.Create("Test", 441, 1, 44100, false);
            _bank = new VoiceBank("Test Voices", 2, loop: true);
        }

        [TearDown]
        public void TearDown()
        {
            _bank.Dispose();
            Object.DestroyImmediate(_cue);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void IsPlaying_BeforeFirstUse_IsFalse()
        {
            Assert.IsFalse(_bank.IsPlaying(0));
        }

        [Test]
        public void Load_FillsTheVoiceFromTheCue()
        {
            var source = _bank.Load(1, _cue, _clip, 1.2f);

            Assert.AreSame(_clip, source.clip);
            Assert.AreEqual(1.2f, source.pitch, 1e-5f);
            Assert.AreEqual(0.4f, source.volume, 1e-5f);
            Assert.AreEqual(42, source.priority);
            Assert.AreEqual(1f, source.spatialBlend);
            Assert.AreEqual((3f, 90f), (source.minDistance, source.maxDistance));
            Assert.IsTrue(source.loop);
            Assert.AreSame(source, _bank[1]);
        }

        [Test]
        public void Dispose_RemovesTheVoices()
        {
            var source = _bank[0];

            _bank.Dispose();

            Assert.IsTrue(source == null);
        }
    }
}
