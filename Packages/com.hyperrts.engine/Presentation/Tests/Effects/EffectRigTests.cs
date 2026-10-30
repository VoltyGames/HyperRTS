using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Tests.Effects
{
    /// <summary>One effect rig: graph bursts thinned by density, spin, burst scales, loop poses and stream graphs.</summary>
    public class EffectRigTests
    {
        private EffectRig _rig;

        [SetUp]
        public void SetUp() => _rig = EffectRigKit.Rig("Test Rig");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rig.gameObject);

        [TestCase(DetailLevel.High)]
        [TestCase(DetailLevel.Low)]
        public void Burst_GraphRig_ThinsItsGraphByDensity(DetailLevel level)
        {
            var graph = EffectRigKit.AddGraph(_rig);

            _rig.Burst(new Vector3(5f, 0f, 3f), Quaternion.identity, 1f, EffectDetail.DensityAt(level));

            Assert.AreEqual(EffectDetail.DensityAt(level), graph.GetFloat(EffectGraph.DensityProperty));
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
        public void Play_ScalesEachBurst_InsideTheRange_GrownByTheSplashRadius()
        {
            _rig.scale = new Vector2(1.5f, 2.5f);
            _rig.scalePerRadius = 0.5f;
            UnityEngine.Random.InitState(7);
            var scales = new float[8];

            for (var i = 0; i < scales.Length; i++)
            {
                _rig.Play(Vector3.zero, Quaternion.identity, 2f);
                scales[i] = _rig.transform.localScale.x;
            }

            Assert.That(scales, Is.All.InRange(2.5f, 3.5f), "scale 1.5 to 2.5 plus 0.5 per metre of 2 m");
            Assert.That(scales, Is.Not.All.EqualTo(scales[0]), "each play rolls its own scale");
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
    }
}
