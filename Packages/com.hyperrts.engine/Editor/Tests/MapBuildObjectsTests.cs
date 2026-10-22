using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Editor.Maps.Scenes;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Tests
{
    /// <summary>A build owns its recorded SubScene instances after their prefab's internal IDs change, and nothing it didn't make.</summary>
    public class MapBuildObjectsTests
    {
        private const string Folder = "Assets/MapBuildObjectsTest";
        private const string Prefab = Folder + "/Prop.prefab";
        private const string Other = Folder + "/Other.prefab";
        private const string ScenePath = Folder + "/Entities.unity";
        private const string Record = Folder + "/Entities_BuildObjects.txt";

        [SetUp]
        public void SetUp() => AssetDatabase.CreateFolder("Assets", "MapBuildObjectsTest");

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void RecordedInstanceThatLostItsGeneratedNameIsStillOwned_AndAnUnrecordedOneIsNot()
        {
            SavePrefab(Prefab, "Child");
            SavePrefab(Other, "Child");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            made.name = $"{MapScene.Generated} Prop 1";
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Other), scene);
            SceneManager.MoveGameObjectToScene(new GameObject(MapScene.Dressing), scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var record = new MapBuildObjects(Record);
            record.Add(made);
            record.Save();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenumberFileIds(Prefab);
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                var buildObjects = new MapBuildObjects(Record);
                var owned = buildObjects.Owned(scene.GetRootGameObjects()).Select(root => root.name).ToList();

                // The name override pointed at the old root ID, so the instance is back to the prefab's name.
                CollectionAssert.AreEqual(new[] { "Prop" }, owned);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        // What converting a prefab to a variant does: same GUID, every object under a new local file ID.
        private static void RenumberFileIds(string path)
        {
            var text = File.ReadAllText(path);
            var renumbered = new Dictionary<string, string>();
            foreach (Match anchor in Regex.Matches(text, @"&(\d+)"))
            {
                renumbered[anchor.Groups[1].Value] = (900001 + renumbered.Count).ToString();
            }

            text = Regex.Replace(text, @"(&|fileID: )(\d+)", match =>
                renumbered.TryGetValue(match.Groups[2].Value, out var id) ? match.Groups[1].Value + id : match.Value);

            File.WriteAllText(path, text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void SavePrefab(string path, string child)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            new GameObject(child).transform.SetParent(root.transform, false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
