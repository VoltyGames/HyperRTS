using System;
using System.Linq;
using HyperRTS.Editor.Environment;
using NUnit.Framework;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Editor.Tests
{
    /// <summary>
    /// The procedural terrain noise, micro detail and water ripple textures: statistics, unit normals and seamless
    /// tiling.
    /// </summary>
    public class EnvironmentTexturesTests
    {
        private Vector3[] terrain;
        private Vector3[] ripples;
        private Color[] micro;

        [OneTimeSetUp]
        public void Bake()
        {
            terrain = EnvironmentTextures.TerrainNoise();
            ripples = EnvironmentTextures.RippleNormals();
            micro = TerrainMicroTexture.Texels();
        }

        [TestCase(4f)]
        [TestCase(40f)]
        public void LowpassIsStandardised(float cycles)
        {
            var random = new Random(1u);
            var field = TileableNoise.Lowpass(64, cycles, ref random);
            var mean = field.Average();
            var std = Math.Sqrt(field.Average(v => (v - mean) * (v - mean)));

            Assert.That(mean, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(std, Is.EqualTo(1.0).Within(1e-3));
        }

        [Test]
        public void TerrainSelectorIsUniform()
        {
            var below = terrain.Count(t => t.x < 0.25f) / (float)terrain.Length;

            Assert.That(terrain.Average(t => t.x), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(below, Is.EqualTo(0.25f).Within(1e-3f));
        }

        [Test]
        public void TerrainBrightnessCentresOnHalf()
        {
            Assert.That(terrain.Average(t => t.y), Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(terrain.Average(t => t.z), Is.EqualTo(0.5f).Within(0.02f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TerrainNoiseTiles(int channel)
        {
            var values = terrain.Select(t => t[channel]).ToArray();

            AssertTiles(values, EnvironmentTextures.TerrainSize);
        }

        [Test]
        public void RippleNormalsAreUnitAndFaceUp()
        {
            Assert.That(ripples.All(n => Mathf.Abs(n.magnitude - 1f) < 1e-4f), Is.True);
            Assert.That(ripples.All(n => n.z > 0f), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void RippleNormalsTile(int axis)
        {
            var values = ripples.Select(n => n[axis]).ToArray();

            AssertTiles(values, EnvironmentTextures.RippleSize);
        }

        [Test]
        public void MicroNormalsTiltModestly()
        {
            var tilt = micro.Average(t => new Vector2(t.r * 2f - 1f, t.g * 2f - 1f).magnitude);

            Assert.That(tilt, Is.InRange(0.15f, 0.6f), "a grain, not a flat or a jagged surface");
            Assert.That(micro.Average(t => t.r), Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(micro.Average(t => t.g), Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void MicroDarkensOnlyItsGapsAndCentresItsAlbedo()
        {
            var open = micro.Count(t => t.b > 0.999f) / (float)micro.Length;

            Assert.That(open, Is.InRange(0.3f, 0.8f), "crests stay unshadowed");
            Assert.That(micro.Min(t => t.b), Is.GreaterThan(0.3f));
            Assert.That(micro.Average(t => t.a), Is.EqualTo(0.5f).Within(0.02f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void MicroTiles(int channel)
        {
            var values = micro.Select(t => t[channel]).ToArray();

            AssertTiles(values, TerrainMicroTexture.Size);
        }

        /// <summary>The step across each wrap edge is no bigger than a typical step between neighbours inside.</summary>
        private static void AssertTiles(float[] values, int size)
        {
            float Step(int x0, int y0, int x1, int y1) => Mathf.Abs(values[y0 * size + x0] - values[y1 * size + x1]);
            var rows = Enumerable.Range(0, size).ToArray();
            var insideU = rows.SelectMany(y => rows.Skip(1).Select(x => Step(x - 1, y, x, y))).Average();
            var insideV = rows.SelectMany(x => rows.Skip(1).Select(y => Step(x, y - 1, x, y))).Average();
            var acrossU = rows.Average(y => Step(size - 1, y, 0, y));
            var acrossV = rows.Average(x => Step(x, size - 1, x, 0));

            Assert.That(acrossU, Is.LessThan(insideU * 1.5f));
            Assert.That(acrossV, Is.LessThan(insideV * 1.5f));
        }
    }
}
