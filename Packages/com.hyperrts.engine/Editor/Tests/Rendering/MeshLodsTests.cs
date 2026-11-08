using HyperRTS.Editor.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Rendering
{
    /// <summary>The size rule: level 1 once a mesh spans the first-level pixels, one more level per halving.</summary>
    public class MeshLodsTests
    {
        [Test]
        public void SizeRule_DropsALevelAtTheFirstLevelPixels_AndAnotherAtHalfThat()
        {
            var mesh = Sphere();
            MeshLodUtility.GenerateMeshLods(mesh, default, 2);
            MeshLods.UseSizeRule(mesh);

            Assert.AreEqual(3, mesh.lodCount);
            Assert.AreEqual(0f, Level(mesh, MeshLods.FirstLevelPixels * 2f), 1e-3f);
            Assert.AreEqual(1f, Level(mesh, MeshLods.FirstLevelPixels), 1e-3f);
            Assert.AreEqual(2f, Level(mesh, MeshLods.FirstLevelPixels / 2f), 1e-3f);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void SizeRule_LeavesAMeshWithOneLevelAlone()
        {
            var mesh = Sphere();
            MeshLods.UseSizeRule(mesh);

            Assert.IsFalse(mesh.lodSelectionCurve.IsValid());
            Object.DestroyImmediate(mesh);
        }

        // The level Entities Graphics picks for a mesh whose bounds' diameter spans this many pixels, at threshold 1.
        private static float Level(Mesh mesh, float pixels) =>
            mesh.lodSelectionCurve.lodSlope * Mathf.Log(1f / pixels, 2f) + mesh.lodSelectionCurve.lodBias;

        private static Mesh Sphere()
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(primitive);
            return mesh;
        }
    }
}
