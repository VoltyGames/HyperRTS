using HyperRTS.Presentation.Audio;
using HyperRTS.Simulation.Audio;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public class SoundPoolTests
    {
        private SoundCue _gun;
        private SoundCue _voice;

        [SetUp]
        public void SetUp()
        {
            _gun = ScriptableObject.CreateInstance<SoundCue>();
            _gun.maxInstances = 2;
            _gun.priority = 200;
            _voice = ScriptableObject.CreateInstance<SoundCue>();
            _voice.maxInstances = 1;
            _voice.priority = 10;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gun);
            Object.DestroyImmediate(_voice);
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
    }
}
