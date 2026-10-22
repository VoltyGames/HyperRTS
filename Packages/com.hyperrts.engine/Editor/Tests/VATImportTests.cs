using System.IO;
using System.Text.RegularExpressions;
using HyperRTS.Editor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Editor.Tests
{
    public class VATImportTests
    {
        private const string Folder = "Assets/_VATImportTests";
        private const string Model = Folder + "/Quad.obj";
        private const string Positions = Folder + "/VAT/Quad_Pos.exr";

        private const string Triangle = "v 0 0 0\nv 1 0 0\nv 1 1 0\nvt 0 0\nvt 1 0\nvt 1 1\nf 1/1 2/2 3/3\n";
        private const string BakeInfo = @"{ ""positions"": ""Quad_Pos.exr"", ""normals"": ""Quad_Nrm.exr"" }";

        [TearDown]
        public void DeleteFolder() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void VATSetImportsWithFullUVsAndExactTexels()
        {
            AssetDatabase.CreateFolder("Assets", "_VATImportTests");
            Directory.CreateDirectory(Folder + "/VAT");
            File.WriteAllText(Folder + "/VAT/Quad.vat.json", BakeInfo);
            File.WriteAllText(Model, Triangle);
            File.WriteAllBytes(Positions, SixColumnEXR());
            // An OBJ carries no UV1, which the rule reports.
            LogAssert.Expect(LogType.Error, new Regex("has no UV1 texel index"));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Positions);

            Assert.IsFalse(importer.generateSecondaryUV);
            Assert.AreEqual(ModelImporterMeshCompression.Off, importer.meshCompression);
            Assert.AreEqual(TextureFormat.RGBAHalf, texture.format);
            Assert.AreEqual((FilterMode.Point, 1), (texture.filterMode, texture.mipmapCount));
        }

        [Test]
        public void LitTeamVertexStagesCompileWithAndWithoutVATAndBuild()
        {
            var shader = Shader.Find("HyperRTS/LitTeam");

            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader));
            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader, "_VAT"));
            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader, "_VAT", "DOTS_INSTANCING_ON"));
            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader, "_BUILD", "DOTS_INSTANCING_ON"));
            Assert.IsEmpty(ShaderChecks.VertexVariantErrors(shader, "_VAT", "_BUILD"));
        }

        private static byte[] SixColumnEXR()
        {
            var texture = new Texture2D(6, 2, TextureFormat.RGBAHalf, false, true);
            try
            {
                return texture.EncodeToEXR(Texture2D.EXRFlags.None);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
