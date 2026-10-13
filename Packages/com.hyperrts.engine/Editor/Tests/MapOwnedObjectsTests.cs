using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HyperRTS.Editor.Maps.Scenes;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Tests
{
    /// <summary>A build still owns its SubScene instances after their library prefab's internal IDs change.</summary>
    public class MapOwnedObjectsTests
    {
        private const string Folder = "Assets/MapOwnedObjectsTest";
        private const string Prefab = Folder + "/Prop.prefab";
        private const string Other = Folder + "/Other.prefab";
        private const string ScenePath = Folder + "/Entities.unity";

        [SetUp]
        public void SetUp() => AssetDatabase.CreateFolder("Assets", "MapOwnedObjectsTest");

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void LibraryInstanceThatLostItsGeneratedNameIsStillOwned()
        {
            SavePrefab(Prefab, "Child");
            SavePrefab(Other, "Child");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var generated = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), scene);
            MapScene.NameGenerated(generated, "Prop 1");
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Other), scene);
            SceneManager.MoveGameObjectToScene(new GameObject(MapScene.Dressing), scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenumberFileIds(Prefab);
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                var library = new HashSet<string> { Prefab };
                var owned = new List<string>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (MapOwnedObjects.IsOwned(root, library))
                    {
                        owned.Add($"{root.name} {PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)}");
                    }
                }

                // The name override pointed at the old root ID, so the instance is back to the prefab's name.
                CollectionAssert.AreEqual(new[] { $"Prop {Prefab}" }, owned);
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
