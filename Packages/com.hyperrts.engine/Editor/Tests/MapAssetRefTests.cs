using HyperRTS.Editor.Maps.Library;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Library asset links load by GUID and keep their readable path in step with it.</summary>
    public class MapAssetRefTests
    {
        private const string Folder = "Assets/MapAssetRefTest";
        private const string Path = Folder + "/Ground.mat";
        private const string Moved = Folder + "/Moved.mat";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "MapAssetRefTest");
            AssetDatabase.CreateAsset(new Material(Shader.Find("Hidden/InternalErrorShader")), Path);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void AnEditedPath_FillsInItsGuid()
        {
            var link = new MapAssetRef { path = Path, guid = "stale" };

            Assert.IsTrue(link.Sync());
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(Path), link.guid);
        }

        [Test]
        public void AMovedAsset_StillLoads_AndSyncWritesItsNewPath()
        {
            var link = MapAssetRef.At(Path);
            AssetDatabase.MoveAsset(Path, Moved);

            Assert.IsNotNull(link.Load(typeof(Material), out _));
            Assert.IsTrue(link.Sync());
            Assert.AreEqual(Moved, link.path);
            Assert.IsFalse(link.Sync(), "a synced link is stable");
        }

        [Test]
        public void ALinkWithoutAGuid_DoesNotLoad_AndSaysWhy()
        {
            var link = new MapAssetRef { path = Path };

            Assert.IsNull(link.Load(typeof(Material), out var problem));
            StringAssert.Contains("has no guid", problem);
        }
    }
}
