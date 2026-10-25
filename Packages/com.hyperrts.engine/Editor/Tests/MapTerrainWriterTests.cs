using HyperRTS.Editor.Maps.Scenes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Scenery models reach the terrain as tree prototypes Unity accepts: each with a LODGroup.</summary>
    public class MapTerrainWriterTests
    {
        private const string Folder = "Assets/MapTerrainWriterTest";
        private const string ModelPath = Folder + "/Stone.prefab";
        private const string CopyPath = Folder + "/Stone_Scenery.prefab";

        [SetUp]
        public void SetUp() => AssetDatabase.CreateFolder("Assets", "MapTerrainWriterTest");

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void AModelWithoutALODGroup_GetsACopyWithOneNeverCulledLOD()
        {
            var model = SaveModel(withLods: false);

            var prototype = MapTerrainWriter.TreeModel(model, CopyPath);

            Assert.AreEqual(CopyPath, AssetDatabase.GetAssetPath(prototype));
            Assert.IsNull(model.GetComponent<LODGroup>(), "the source model stays as it was");
            var lods = prototype.GetComponent<LODGroup>().GetLODs();
            Assert.AreEqual(1, lods.Length);
            Assert.AreEqual(0f, lods[0].screenRelativeTransitionHeight);
            Assert.AreEqual(1, lods[0].renderers.Length);
        }

        [Test]
        public void AModelWithALODGroup_IsUsedAsItIs()
        {
            var model = SaveModel(withLods: true);

            Assert.AreSame(model, MapTerrainWriter.TreeModel(model, CopyPath));
            Assert.IsFalse(System.IO.File.Exists(CopyPath));
        }

        private static GameObject SaveModel(bool withLods)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                if (withLods)
                {
                    root.AddComponent<LODGroup>();
                }

                return PrefabUtility.SaveAsPrefabAsset(root, ModelPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
