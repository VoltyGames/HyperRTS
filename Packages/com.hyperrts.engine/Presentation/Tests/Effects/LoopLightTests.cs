using System.Linq;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Effects
{
    /// <summary>
    /// Streamed loops in the scene's light: fire lights go to the nearest burning loops within the cap on High only, and
    /// smoke shadows reach the sun's cookie from Medium up, growing in with the smoke.
    /// </summary>
    public class LoopLightTests
    {
        private GameObject _root;
        private EffectRig _fire;
        private EffectRig _dust;
        private LoopLights _lights;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Loop Lights");
            _lights = new LoopLights(_root.transform);
            _fire = EffectRigKit.Rig("Fire");
            _fire.gameObject.AddComponent<LoopLight>().height = 2f;
            _fire.gameObject.AddComponent<SmokeShadow>();
            _dust = EffectRigKit.Rig("Dust");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_fire.gameObject);
            Object.DestroyImmediate(_dust.gameObject);
        }

        [Test]
        public void FireLights_GoToTheNearestFires_UpToTheCap()
        {
            for (var i = 11; i >= 0; i--)
            {
                _lights.Offers.Add(_fire, new Vector3(i * 10f, 0f, 0f), 1f, (uint)i, 100f);
                _lights.Offers.Add(_dust, new Vector3(i, 0f, 0f), 1f, (uint)i, 100f);
            }

            _lights.Show(Vector3.zero, DetailLevel.High, 0f);

            var lit = Lit().Select(light => light.transform.position).OrderBy(position => position.x).ToArray();
            Assert.AreEqual(LoopLights.Capacity, lit.Length);
            for (var i = 0; i < lit.Length; i++)
            {
                Assert.AreEqual(new Vector3(i * 10f, 2f, 0f), lit[i], "the nearest fires, at the light's height");
            }
        }

        [TestCase(DetailLevel.Low)]
        [TestCase(DetailLevel.Medium)]
        public void FireLights_StayOutBelowHigh_AndGoOutWithTheFire(DetailLevel level)
        {
            _lights.Offers.Add(_fire, Vector3.zero, 1f, 0u, 100f);
            _lights.Show(Vector3.zero, DetailLevel.High, 0f);
            Assert.AreEqual(1, Lit().Length);

            _lights.Offers.Add(_fire, Vector3.zero, 1f, 0u, 100f);
            _lights.Show(Vector3.zero, level, 0f);
            Assert.AreEqual(0, Lit().Length, "below High");

            _lights.Show(Vector3.zero, DetailLevel.High, 0f);
            Assert.AreEqual(0, Lit().Length, "no fire offered");
        }

        [Test]
        public void FireLight_Flickers_AroundItsStrength()
        {
            var source = _fire.GetComponent<LoopLight>();
            var samples = Enumerable.Range(0, 50).Select(i => source.IntensityAt(i * 0.07f, 1f, 0.5f)).ToArray();

            Assert.Greater(samples.Max() - samples.Min(), 0.1f, "it flickers");
            Assert.LessOrEqual(samples.Max(), source.intensity * 0.5f, "never above its strength");
            Assert.GreaterOrEqual(samples.Min(), source.intensity * 0.5f * (1f - source.flicker));
        }

        [TestCase(DetailLevel.Low, 0)]
        [TestCase(DetailLevel.Medium, SmokeShadows.Capacity)]
        [TestCase(DetailLevel.High, SmokeShadows.Capacity)]
        public void SmokeShadows_ReachTheCookie_FromMediumUp_NearestFirst(DetailLevel level, int expected)
        {
            var smoke = new SmokeShadows();
            for (var i = SmokeShadows.Capacity + 7; i >= 0; i--)
            {
                smoke.Offers.Add(_fire, new Vector3(i * 10f, 0f, 0f), 1f, (uint)i, 100f);
                smoke.Offers.Add(_dust, Vector3.zero, 1f, (uint)i, 100f);
            }

            smoke.Show(Vector3.zero, level);

            Assert.AreEqual(expected, smoke.Count);
            Assert.AreEqual(expected, Shader.GetGlobalInt(SmokeShadows.CountId));
            if (expected > 0)
            {
                var farthest = Shader.GetGlobalVectorArray(SmokeShadows.StartsId).Take(expected).Max(start => start.x);
                Assert.AreEqual((expected - 1) * 10f, farthest, "the nearest columns");
            }
        }

        [Test]
        public void SmokeColumn_GrowsInWithItsBillows_AndFollowsTheLoopsStrength()
        {
            var column = _fire.GetComponent<SmokeShadow>();

            column.Column(Vector3.zero, 0f, 1f, out _, out var fresh);
            column.Column(Vector3.zero, column.growSeconds * 0.5f, 1f, out _, out var growing);
            column.Column(Vector3.zero, column.growSeconds, 1f, out var start, out var grown);
            column.Column(Vector3.zero, column.growSeconds, 0.5f, out _, out var fading);

            Assert.AreEqual(0f, fresh.w, "no shadow before the smoke");
            Assert.That(growing.w, Is.GreaterThan(0f).And.LessThan(grown.w));
            Assert.Less(growing.y, grown.y, "the column rises as it grows");
            Assert.AreEqual(column.opacity, grown.w, 1e-5f);
            Assert.AreEqual(column.radius, start.w, 1e-5f);
            Assert.AreEqual(grown.w * 0.5f, fading.w, 1e-5f, "fades with the loop");
        }

        private Light[] Lit() => _root.GetComponentsInChildren<Light>().Where(light => light.enabled).ToArray();
    }
}
