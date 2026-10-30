using System;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Tests.Effects
{
    /// <summary>One effect rig: burst counts and timing, spin, sizes, loop poses, streamed particles and graphs.</summary>
    public class EffectRigTests
    {
        private EffectRig _rig;

        [SetUp]
        public void SetUp() => _rig = EffectRigKit.Rig("Test Rig");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rig.gameObject);

        private ParticleSystem Particles => EffectRigKit.System(_rig);

        [TestCase(20, 1f, 20)]
        [TestCase(20, 0.35f, 7)]
        [TestCase(1, 0.35f, 1)]
        public void Burst_EmitsTheAuthoredCount_ThinnedByDensity_NeverToNothing(int count, float density,
            int expected)
        {
            EffectRigKit.AddBurst(_rig, 0f, (short)count);

            _rig.Burst(Vector3.zero, Quaternion.identity, 1f, density);

            Assert.AreEqual(expected, Particles.particleCount);
        }

        [TestCase(DetailLevel.High)]
        [TestCase(DetailLevel.Low)]
        public void Burst_GraphRig_ThinsItsGraphByDensity(DetailLevel level)
        {
            var graph = EffectRigKit.AddGraph(_rig);

            _rig.Burst(new Vector3(5f, 0f, 3f), Quaternion.identity, 1f, EffectDetail.DensityAt(level));

            Assert.AreEqual(EffectDetail.DensityAt(level), graph.GetFloat(EffectGraph.DensityProperty));
        }

        [Test]
        public void TimedBurst_FiresOnceDue_AtTheBurstPose()
        {
            EffectRigKit.AddBurst(_rig, 0.5f, 10);
            var pose = new Vector3(5f, 0f, 3f);

            _rig.Burst(pose, Quaternion.identity, 1f, 1f);
            _rig.transform.position = Vector3.zero;
            _rig.Tick(0.3f);
            Assert.AreEqual(0, Particles.particleCount, "not yet due");

            _rig.Tick(0.3f);
            var particles = new ParticleSystem.Particle[16];
            Assert.AreEqual(10, Particles.GetParticles(particles));
            Assert.That(Vector3.Distance(pose, particles[0].position), Is.LessThan(1e-3f));
        }

        [Test]
        public void RepeatingBurst_FiresEachCycleAtItsInterval()
        {
            EffectRigKit.AddBurst(_rig, 0f, 5, 3, 0.5f);

            _rig.Burst(Vector3.zero, Quaternion.identity, 1f, 1f);
            Assert.AreEqual(5, Particles.particleCount);

            _rig.Tick(0.6f);
            Assert.AreEqual(10, Particles.particleCount);

            _rig.Tick(0.5f);
            _rig.Tick(1f);
            Assert.AreEqual(15, Particles.particleCount, "three cycles, no more");
        }

        [Test]
        public void Burst_KeepsTheAim_OrWithSpinTurnsAboutTheRigUp()
        {
            var aim = Quaternion.Euler(30f, 70f, 0f);
            _rig.Burst(Vector3.zero, aim, 1f, 1f);
            Assert.That(Quaternion.Angle(aim, _rig.transform.rotation), Is.LessThan(1e-2f));

            _rig.randomSpin = true;
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
        public void Burst_SizesItsParticles_AndLeavesEarlierOnesAlone()
        {
            SizedBurst(2f);

            _rig.Burst(Vector3.zero, Quaternion.identity, 0.5f, 1f);
            _rig.Burst(Vector3.zero, Quaternion.identity, 2f, 1f);

            CollectionAssert.AreEquivalent(new[] { 1f, 4f }, Sizes());
            Assert.AreEqual(Vector3.one, _rig.transform.localScale);
        }

        [Test]
        public void Play_SizesEachBurst_InsideTheScaleRange()
        {
            SizedBurst(2f);
            _rig.scale = new Vector2(1.5f, 2.5f);
            UnityEngine.Random.InitState(7);

            for (var i = 0; i < 8; i++)
            {
                _rig.Play(Vector3.zero, Quaternion.identity);
            }

            var sizes = Sizes();
            Assert.AreEqual(8, sizes.Length);
            Assert.That(sizes, Is.All.InRange(3f, 5f));
            Assert.That(sizes, Is.Not.All.EqualTo(sizes[0]), "each play rolls its own scale");
        }

        [Test]
        public void Play_GrowsTheBurst_WithTheSplashRadius()
        {
            SizedBurst(2f);
            _rig.scalePerRadius = 0.5f;

            _rig.Play(Vector3.zero, Quaternion.identity, 4f);

            Assert.AreEqual(6f, Sizes()[0], 1e-4f, "scale 1 plus 0.5 per metre of 4 m");
        }

        [Test]
        public void PreparedSystems_AlwaysSimulate_WithThePoolCap()
        {
            var main = Particles.main;
            main.maxParticles = 300;

            _rig.Prepare();

            Assert.AreEqual(ParticleSystemCullingMode.AlwaysSimulate, main.cullingMode);
            Assert.AreEqual(EffectEmitter.MaxParticles, main.maxParticles);
        }

        [Test]
        public void Stream_PosesAtTheGivenScale()
        {
            EffectRigKit.SetRates(_rig, 10f, 0f);
            var main = Particles.main;
            main.startSize = 2f;
            var carry = 0f;

            _rig.Stream(Vector3.zero, Vector3.zero, Quaternion.identity, 1f, 1f, 1f, ref carry, 1.5f);

            Assert.That(Sizes(), Is.All.EqualTo(3f).Within(1e-4f));
            Assert.AreEqual(Vector3.one, _rig.transform.localScale, "live particles must not follow the next pose");
        }

        [Test]
        public void Stream_CarriesTheFractionToTheNextFrame()
        {
            EffectRigKit.SetRates(_rig, 10f, 0f);
            var carry = 0f;

            _rig.Stream(Vector3.zero, Vector3.zero, Quaternion.identity, 0.05f, 1f, 1f, ref carry);
            Assert.AreEqual(0, Particles.particleCount);
            Assert.AreEqual(0.5f, carry, 1e-4f);

            _rig.Stream(Vector3.zero, Vector3.zero, Quaternion.identity, 0.05f, 1f, 1f, ref carry);
            Assert.AreEqual(1, Particles.particleCount);
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

            Assert.Greater(Particles.particleCount, 60);
            Assert.Greater(second.particleCount, 60);
        }

        [Test]
        public void Stream_SpreadsParticlesEvenlyAlongThePath()
        {
            EffectRigKit.SetRates(_rig, 0f, 1f);
            var carry = 0f;

            _rig.Stream(Vector3.zero, new Vector3(4f, 0f, 0f), Quaternion.identity, 0f, 1f, 1f, ref carry);

            var particles = new ParticleSystem.Particle[8];
            Assert.AreEqual(4, Particles.GetParticles(particles));
            var xs = new float[4];
            for (var i = 0; i < 4; i++)
            {
                xs[i] = particles[i].position.x;
            }

            Array.Sort(xs);
            Assert.That(xs, Is.EqualTo(new[] { 1f, 2f, 3f, 4f }).Within(1e-3f));
        }

        [Test]
        public void StreamGraph_PlaysItsOwnCopy_FedTheSpeedAndTheThinnedDensity()
        {
            var template = EffectRigKit.AddGraph(_rig, EffectRigKit.TestStreamGraph);
            var streams = new EffectStreams();
            Step(streams, Vector3.zero).Emit(Quaternion.identity, 0.5f, 0.5f);

            var state = Step(streams, new Vector3(4f, 0f, 0f));
            state.Emit(Quaternion.identity, 0.5f, 0.5f);

            var graphs = _rig.GetComponentsInChildren<VisualEffect>();
            Assert.AreEqual(2, graphs.Length, "the template and one stream's copy");
            var copy = graphs[0] == template ? graphs[1] : graphs[0];
            Assert.AreEqual(8f, copy.GetFloat(GraphStream.SpeedProperty), 1e-4f, "4 m in 0.5 s");
            Assert.AreEqual(0.5f * EffectDetail.Density, copy.GetFloat(EffectGraph.DensityProperty), 1e-4f);
            Assert.AreEqual(new Vector3(4f, 0f, 0f), copy.GetVector3(GraphStream.ToProperty));
            Assert.AreEqual(0f, template.GetFloat(GraphStream.SpeedProperty), "the template only bursts");
        }

        private StreamState Step(EffectStreams streams, Vector3 position)
        {
            streams.BeginFrame();
            var state = streams.Step(Entity.Null, 0, position);
            state.Use(_rig);
            streams.EndFrame();
            return state;
        }

        private void SizedBurst(float startSize)
        {
            EffectRigKit.AddBurst(_rig, 0f, 1);
            var main = Particles.main;
            main.startSize = startSize;
        }

        private float[] Sizes()
        {
            var particles = new ParticleSystem.Particle[Particles.particleCount];
            Particles.GetParticles(particles);
            return Array.ConvertAll(particles, particle => particle.startSize);
        }
    }
}
