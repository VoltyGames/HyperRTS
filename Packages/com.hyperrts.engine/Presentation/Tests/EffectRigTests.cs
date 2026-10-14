using System;
using HyperRTS.Presentation.Effects;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Tests
{
    public class EffectRigTests
    {
        private EffectRig _rig;

        [SetUp]
        public void SetUp() => _rig = EffectRigKit.Rig("Test Rig");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rig.gameObject);

        [Test]
        public void Burst_EmitsTheAuthoredCount_ThinnedByDensity()
        {
            EffectRigKit.AddBurst(_rig, 0f, 20);

            _rig.Burst(Vector3.zero, Quaternion.identity, 1f, 0.35f);
            Assert.AreEqual(7, EffectRigKit.System(_rig).particleCount);

            _rig.Burst(Vector3.zero, Quaternion.identity, 1f, 1f);
            Assert.AreEqual(27, EffectRigKit.System(_rig).particleCount);
        }

        [Test]
        public void Burst_ThinnedToNothing_StillEmitsOne()
        {
            EffectRigKit.AddBurst(_rig, 0f, 1);

            _rig.Burst(Vector3.zero, Quaternion.identity, 1f, 0.35f);

            Assert.AreEqual(1, EffectRigKit.System(_rig).particleCount);
        }

        [Test]
        public void TimedBurst_FiresOnceDue_AtTheBurstPose()
        {
            EffectRigKit.AddBurst(_rig, 0.5f, 10);
            var pose = new Vector3(5f, 0f, 3f);

            _rig.Burst(pose, Quaternion.identity, 1f, 1f);
            _rig.transform.position = Vector3.zero;
            _rig.Tick(0.3f);
            Assert.AreEqual(0, EffectRigKit.System(_rig).particleCount, "not yet due");

            _rig.Tick(0.3f);
            var particles = new ParticleSystem.Particle[16];
            Assert.AreEqual(10, EffectRigKit.System(_rig).GetParticles(particles));
            Assert.That(Vector3.Distance(pose, particles[0].position), Is.LessThan(1e-3f));
        }

        [Test]
        public void Stream_CarriesTheFractionToTheNextFrame()
        {
            EffectRigKit.SetRates(_rig, 10f, 0f);
            var carry = 0f;

            _rig.Stream(Vector3.zero, Vector3.zero, Quaternion.identity, 0.05f, 1f, 1f, ref carry);
            Assert.AreEqual(0, EffectRigKit.System(_rig).particleCount);
            Assert.AreEqual(0.5f, carry, 1e-4f);

            _rig.Stream(Vector3.zero, Vector3.zero, Quaternion.identity, 0.05f, 1f, 1f, ref carry);
            Assert.AreEqual(1, EffectRigKit.System(_rig).particleCount);
            Assert.AreEqual(0f, carry, 1e-4f);
        }

        [Test]
        public void Stream_OneParticleAFrame_FeedsEverySystem()
        {
            EffectRigKit.SetRates(_rig, 10f, 0f);
            var second = EffectRigKit.AddSystem(_rig, "Second");
            EffectRigKit.SetRates(second, 10f, 0f);
            var carry = 0f;

            for (var frame = 0; frame < 300; frame++)
            {
                _rig.Stream(Vector3.zero, Vector3.zero, Quaternion.identity, 1f / 30f, 1f, 1f, ref carry);
            }

            Assert.Greater(EffectRigKit.System(_rig).particleCount, 60);
            Assert.Greater(second.particleCount, 60);
        }

        [Test]
        public void Stream_SpreadsParticlesEvenlyAlongThePath()
        {
            EffectRigKit.SetRates(_rig, 0f, 1f);
            var carry = 0f;

            _rig.Stream(Vector3.zero, new Vector3(4f, 0f, 0f), Quaternion.identity, 0f, 1f, 1f, ref carry);

            var particles = new ParticleSystem.Particle[8];
            Assert.AreEqual(4, EffectRigKit.System(_rig).GetParticles(particles));
            var xs = new float[4];
            for (var i = 0; i < 4; i++)
            {
                xs[i] = particles[i].position.x;
            }

            Array.Sort(xs);
            Assert.That(xs, Is.EqualTo(new[] { 1f, 2f, 3f, 4f }).Within(1e-3f));
        }
    }
}
