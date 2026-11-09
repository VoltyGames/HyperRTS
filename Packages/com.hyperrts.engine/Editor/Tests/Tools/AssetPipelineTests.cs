using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Editor.Environment;
using HyperRTS.Editor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Editor.Tests.Tools
{
    /// <summary>Generated environment textures tile and stay balanced, and VAT models and their motion import for the LitTeam shader.</summary>
    public class AssetPipelineTests
    {
        private const string VATFolder = "Assets/_VATImportTests";
        private const string VATModel = VATFolder + "/Quad.obj";
        private const string VATMotion = VATFolder + "/VAT/Quad_Motion.obj";
        private const string Triangle = "v 0 0 0\nv 1 0 0\nv 1 1 0\nvt 0 0\nvt 1 0\nvt 1 1\nf 1/1 2/2 3/3\n";

        private Vector3[] _terrain;
        private Vector3[] _ripples;
        private Color[] _micro;

        [OneTimeSetUp]
        public void Bake()
        {
            _terrain = EnvironmentTextures.TerrainNoise();
            _ripples = EnvironmentTextures.RippleNormals();
            _micro = TerrainMicroTexture.Texels();
        }

        [TearDown]
        public void DeleteVATFolder() => AssetDatabase.DeleteAsset(VATFolder);

        [Test]
        public void LowpassIsStandardised()
        {
            var random = new Random(1u);
            var field = TileableNoise.Lowpass(64, 4f, ref random);
            var mean = field.Average();
            var std = Math.Sqrt(field.Average(v => (v - mean) * (v - mean)));

            Assert.That(mean, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(std, Is.EqualTo(1.0).Within(1e-3));
        }

        [Test]
        public void FbmRepeatsAtItsPeriod()
        {
            var point = new Vector2(0.37f, 1.21f);

            Assert.That(TileableNoise.Fbm(point + Vector2.right * 4f, 4, 4, 3), Is.EqualTo(TileableNoise.Fbm(point, 4, 4, 3)).Within(1e-4f));
            Assert.That(TileableNoise.Fbm(point + Vector2.up * 4f, 4, 4, 3), Is.EqualTo(TileableNoise.Fbm(point, 4, 4, 3)).Within(1e-4f));
        }

        [Test]
        public void TerrainSelectorIsUniform_AndBrightnessCentresOnHalf()
        {
            var below = _terrain.Count(t => t.x < 0.25f) / (float)_terrain.Length;

            Assert.That(_terrain.Average(t => t.x), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(below, Is.EqualTo(0.25f).Within(1e-3f));
            Assert.That(_terrain.Average(t => t.y), Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(_terrain.Average(t => t.z), Is.EqualTo(0.5f).Within(0.02f));
        }

        [Test]
        public void MicroNormalsTiltModestly_AndDarkenOnlyTheirGaps()
        {
            var tilt = _micro.Average(t => new Vector2(t.r * 2f - 1f, t.g * 2f - 1f).magnitude);
            var open = _micro.Count(t => t.b > 0.999f) / (float)_micro.Length;

            Assert.That(tilt, Is.InRange(0.15f, 0.6f), "a grain, not a flat or a jagged surface");
            Assert.That(open, Is.InRange(0.3f, 0.8f), "crests stay unshadowed");
        }

        [TestCaseSource(nameof(Channels))]
        public void TexturesTile(string texture, int channel)
        {
            switch (texture)
            {
                case "Terrain":
                    AssertTiles(_terrain.Select(t => t[channel]).ToArray(), EnvironmentTextures.TerrainSize);
                    break;
                case "Ripples":
                    AssertTiles(_ripples.Select(n => n[channel]).ToArray(), EnvironmentTextures.RippleSize);
                    break;
                default:
                    AssertTiles(_micro.Select(t => t[channel]).ToArray(), TerrainMicroTexture.Size);
                    break;
            }
        }

        [Test]
        public void VATModelImportsWithFullUVsAndItsMotionUnreduced()
        {
            AssetDatabase.CreateFolder("Assets", "_VATImportTests");
            Directory.CreateDirectory(VATFolder + "/VAT");
            File.WriteAllText(VATMotion, Triangle);
            File.WriteAllText(VATModel, Triangle);
            // An OBJ carries no UV1, which the rule reports.
            LogAssert.Expect(LogType.Error, new Regex("has no mesh with a UV1 texel index"));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var model = (ModelImporter)AssetImporter.GetAtPath(VATModel);
            var motion = (ModelImporter)AssetImporter.GetAtPath(VATMotion);

            Assert.IsFalse(model.generateSecondaryUV);
            Assert.AreEqual(ModelImporterMeshCompression.Off, model.meshCompression);
            Assert.AreEqual(ModelImporterAnimationCompression.Off, motion.animationCompression);
            Assert.IsTrue(motion.resampleCurves);
        }

        [Test]
        public void LitTeamVertexStagesCompileWithRigidVATBonesAndBuild()
        {
            var shader = Shader.Find("HyperRTS/LitTeam");

            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader));
            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader, "_VAT_RIGID"));
            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader, "_BONES"));
            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader, "_BONES", "DOTS_INSTANCING_ON"));
            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader, "_VAT_RIGID", "DOTS_INSTANCING_ON"));
            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader, "_BUILD", "DOTS_INSTANCING_ON"));
            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader, "_VAT_RIGID", "_BUILD"));
        }

        private static IEnumerable<TestCaseData> Channels()
        {
            foreach (var channel in Enumerable.Range(0, 3))
            {
                yield return new TestCaseData("Terrain", channel);
            }

            foreach (var channel in Enumerable.Range(0, 2))
            {
                yield return new TestCaseData("Ripples", channel);
            }

            foreach (var channel in Enumerable.Range(0, 4))
            {
                yield return new TestCaseData("Micro", channel);
            }
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
