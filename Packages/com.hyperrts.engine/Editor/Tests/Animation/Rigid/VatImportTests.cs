using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Editor.Tests.Animation.Rigid
{
    /// <summary>VAT models and their motion import for the LitTeam shader.</summary>
    public class VatImportTests
    {
        private const string VATFolder = "Assets/_VATImportTests";
        private const string VATModel = VATFolder + "/Quad.obj";
        private const string VATMotion = VATFolder + "/VAT/Quad_Motion.obj";
        private const string Triangle = "v 0 0 0\nv 1 0 0\nv 1 1 0\nvt 0 0\nvt 1 0\nvt 1 1\nf 1/1 2/2 3/3\n";

        [TearDown]
        public void DeleteVATFolder() => AssetDatabase.DeleteAsset(VATFolder);

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
    }
}
