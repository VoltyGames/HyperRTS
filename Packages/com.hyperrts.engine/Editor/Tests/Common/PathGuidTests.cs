using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD.Commands;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace HyperRTS.Editor.Tests.Common
{
    /// <summary>Assets made through <see cref="PathGuid"/> take the same GUID on every rebuild, and existing ones keep theirs.</summary>
    public class PathGuidTests
    {
        private const string Folder = "Assets/_PathGuidTests";

        [SetUp]
        public void SetUp() => AssetDatabase.CreateFolder("Assets", "_PathGuidTests");

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void AnAssetRecreatedAfterDeletion_LoadsUnderTheSameGuid()
        {
            var path = new PathGuid(Folder + "/Made.asset");

            Assert.IsNotNull(path.Create<CommandCatalog>());
            AssetDatabase.DeleteAsset(path.Path);
            var again = path.Create<CommandCatalog>();

            Assert.IsNotNull(again);
            Assert.AreEqual(path.Value, AssetDatabase.AssetPathToGUID(path.Path));
            Assert.AreNotEqual(path.Value, new PathGuid(Folder + "/Other.asset").Value);
        }

        [Test]
        public void ANewSceneTakesTheDerivedGuid_AndAnExistingOneKeepsItsOwn()
        {
            var made = new PathGuid(Folder + "/Made.unity");
            var kept = new PathGuid(Folder + "/Kept.unity");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, kept.Path);
            var own = AssetDatabase.AssetPathToGUID(kept.Path);

            made.SaveScene(scene);
            kept.SaveScene(scene);

            Assert.AreEqual(made.Value, AssetDatabase.AssetPathToGUID(made.Path));
            Assert.AreEqual(own, AssetDatabase.AssetPathToGUID(kept.Path));
        }
    }
}
