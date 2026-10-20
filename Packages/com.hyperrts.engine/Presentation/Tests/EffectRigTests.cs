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
        public void RepeatingBurst_FiresEachCycleAtItsInterval()
        {
            EffectRigKit.AddBurst(_rig, 0f, 5, 3, 0.5f);

            _rig.Burst(Vector3.zero, Quaternion.identity, 1f, 1f);
            Assert.AreEqual(5, EffectRigKit.System(_rig).particleCount);

            _rig.Tick(0.6f);
            Assert.AreEqual(10, EffectRigKit.System(_rig).particleCount);

            _rig.Tick(0.5f);
            _rig.Tick(1f);
            Assert.AreEqual(15, EffectRigKit.System(_rig).particleCount, "three cycles, no more");
        }

        [Test]
        public void Burst_WithoutSpin_KeepsTheAim()
        {
            var aim = Quaternion.Euler(30f, 70f, 0f);

            _rig.Burst(Vector3.zero, aim, 1f, 1f);

            Assert.That(Quaternion.Angle(aim, _rig.transform.rotation), Is.LessThan(1e-2f));
        }

        [Test]
        public void Burst_WithSpin_TurnsAboutTheRigUp()
        {
            _rig.randomSpin = true;
            var aim = Quaternion.Euler(30f, 70f, 0f);
            var turned = false;

            for (var i = 0; i < 20; i++)
            {
                _rig.Burst(Vector3.zero, aim, 1f, 1f);
                Assert.That(Vector3.Angle(aim * Vector3.up, _rig.transform.up), Is.LessThan(1e-2f));
                turned |= Vector3.Angle(aim * Vector3.forward, _rig.transform.forward) > 1f;
            }

            Assert.IsTrue(turned);
        }

        [Test]
        public void Variant_PicksTheRigOrAnAlternativeByKey()
        {
            var other = EffectRigKit.Rig("Other");
            try
            {
                _rig.Alternatives = new[] { other };

                Assert.AreSame(_rig, _rig.Pick(0u));
                Assert.AreSame(other, _rig.Pick(1u));
                Assert.AreSame(_rig, _rig.Pick(2u));
            }
            finally
            {
                Object.DestroyImmediate(other.gameObject);
            }
        }

        [Test]
        public void LoopPose_IsStablePerKey_InsideTheScaleRange()
        {
            _rig.scale = new Vector2(0.8f, 1.4f);
            _rig.randomSpin = true;
            var scales = new float[16];

            for (var key = 0u; key < scales.Length; key++)
            {
                scales[key] = _rig.LoopScale(key);
                Assert.AreEqual(scales[key], _rig.LoopScale(key));
                Assert.That(scales[key], Is.InRange(0.8f, 1.4f));
                Assert.AreEqual(_rig.LoopRotation(Quaternion.identity, key),
                    _rig.LoopRotation(Quaternion.identity, key));
            }

            Assert.AreNotEqual(scales[0], scales[1]);
            Assert.AreNotEqual(_rig.LoopRotation(Quaternion.identity, 0u), _rig.LoopRotation(Quaternion.identity, 1u));
        }

        [Test]
        public void Stream_PosesAtTheGivenScale()
        {
            EffectRigKit.SetRates(_rig, 10f, 0f);
            var carry = 0f;

            var main = EffectRigKit.System(_rig).main;
            main.startSize = 2f;

            _rig.Stream(Vector3.zero, Vector3.zero, Quaternion.identity, 1f, 1f, 1f, ref carry, 1.5f);

            Assert.That(Sizes(EffectRigKit.System(_rig)), Is.All.EqualTo(3f).Within(1e-4f));
            Assert.AreEqual(Vector3.one, _rig.transform.localScale, "live particles must not follow the next pose");
        }

        [Test]
        public void Burst_SizesItsParticles_AndLeavesEarlierOnesAlone()
        {
            EffectRigKit.AddBurst(_rig, 0f, 1);
            var main = EffectRigKit.System(_rig).main;
            main.startSize = 2f;

            _rig.Burst(Vector3.zero, Quaternion.identity, 0.5f, 1f);
            _rig.Burst(Vector3.zero, Quaternion.identity, 2f, 1f);

            CollectionAssert.AreEquivalent(new[] { 1f, 4f }, Sizes(EffectRigKit.System(_rig)));
            Assert.AreEqual(Vector3.one, _rig.transform.localScale);
        }

        [Test]
        public void Play_SizesEachBurst_InsideTheScaleRange()
        {
            EffectRigKit.AddBurst(_rig, 0f, 1);
            var main = EffectRigKit.System(_rig).main;
            main.startSize = 2f;
            _rig.scale = new Vector2(1.5f, 2.5f);
            UnityEngine.Random.InitState(7);

            for (var i = 0; i < 8; i++)
            {
                _rig.Play(Vector3.zero, Quaternion.identity);
            }

            var sizes = Sizes(EffectRigKit.System(_rig));
            Assert.AreEqual(8, sizes.Length);
            Assert.That(sizes, Is.All.InRange(3f, 5f));
            Assert.That(sizes, Is.Not.All.EqualTo(sizes[0]), "each play rolls its own scale");
        }

        [Test]
        public void Play_GrowsTheBurst_WithTheSplashRadius()
        {
            EffectRigKit.AddBurst(_rig, 0f, 1);
            var main = EffectRigKit.System(_rig).main;
            main.startSize = 2f;
            _rig.scalePerRadius = 0.5f;

            _rig.Play(Vector3.zero, Quaternion.identity, 4f);

            Assert.AreEqual(6f, Sizes(EffectRigKit.System(_rig))[0], 1e-4f, "scale 1 plus 0.5 per metre of 4 m");
        }

        [Test]
        public void PreparedSystems_AlwaysSimulate_WithThePoolCap()
        {
            var main = EffectRigKit.System(_rig).main;
            main.maxParticles = 300;

            _rig.Prepare();

            Assert.AreEqual(ParticleSystemCullingMode.AlwaysSimulate, main.cullingMode);
            Assert.AreEqual(EffectEmitter.MaxParticles, main.maxParticles);
        }

        private static float[] Sizes(ParticleSystem system)
        {
            var particles = new ParticleSystem.Particle[system.particleCount];
            system.GetParticles(particles);
            return Array.ConvertAll(particles, particle => particle.startSize);
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
