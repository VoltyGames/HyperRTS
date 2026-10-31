using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Scenes;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>What a map build writes into its scenes: Match sides and players, owned objects and terrain trees.</summary>
    public class MapSceneTests : TemplateFixture
    {
        private const string Folder = "Assets/MapSceneTest";

        [SetUp]
        public void SetUp() => AssetDatabase.CreateFolder("Assets", "MapSceneTest");

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void Sides_ResolveKitKeysToPrefabs()
        {
            var (palette, prefab) = Palette();

            var sides = MapMatchContent.Sides(palette);

            CollectionAssert.AreEqual(new byte[] { 1, 3 }, sides.Select(s => s.side));
            var item = sides[1].startKit.Single();
            Assert.AreSame(prefab, item.prefab);
            Assert.AreEqual(new Vector2(1f, 2f), item.offset);
            Assert.AreEqual(45f, item.yaw);
            Assert.IsNull(sides[1].buildOrder);
        }

        [Test]
        public void Players_StandOnTheGroundAtTheirSpawns_AndTakeTurnsAtTheSides()
        {
            var spawns = new List<MapSpawn>
            {
                new(new Vector2(-50f, 0f), 90f, 1, 1, 0),
                new(new Vector2(50f, 0f), 270f, 2, 2, 1),
                new(new Vector2(0f, 50f), 180f, 3, 1, 2),
            };

            var players = MapMatchContent.Players(spawns, Palette().Palette, point => point.x * 0.1f);

            CollectionAssert.AreEqual(new byte[] { 1, 3, 1 }, players.Select(p => p.side));
            Assert.AreEqual(new Vector3(50f, 5f, 0f), players[1].spawnPosition);
            Assert.AreEqual(270f, players[1].spawnYaw);
            Assert.AreEqual(2, players[1].team);
        }

        [Test]
        public void RecordedInstanceThatLostItsGeneratedNameIsStillOwned_AndAnUnrecordedOneIsNot()
        {
            const string prefabPath = Folder + "/Prop.prefab";
            const string scenePath = Folder + "/Entities.unity";
            const string recordPath = Folder + "/Entities_BuildObjects.txt";
            SavePrefab(prefabPath);
            SavePrefab(Folder + "/Other.prefab");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var made = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), scene);
            made.name = $"{MapScene.Generated} Prop 1";
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Other.prefab"), scene);
            SceneManager.MoveGameObjectToScene(new GameObject(MapScene.Dressing), scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            var record = new MapBuildObjects(recordPath);
            record.Add(made);
            record.Save();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenumberFileIds(prefabPath);
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            try
            {
                var owned = new MapBuildObjects(recordPath).Owned(scene.GetRootGameObjects()).Select(root => root.name);

                // The name override pointed at the old root ID, so the instance is back to the prefab's name.
                CollectionAssert.AreEqual(new[] { "Prop" }, owned.ToList());
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void ATreeModelWithoutALODGroup_GetsACopyWithOneNeverCulledLOD()
        {
            const string copyPath = Folder + "/Stone_Scenery.prefab";
            var model = SaveModel(withLods: false);

            var prototype = MapTerrainWriter.TreeModel(model, copyPath);

            Assert.AreEqual(copyPath, AssetDatabase.GetAssetPath(prototype));
            Assert.IsNull(model.GetComponent<LODGroup>(), "the source model stays as it was");
            var lods = prototype.GetComponent<LODGroup>().GetLODs();
            Assert.AreEqual(1, lods.Length);
            Assert.AreEqual(0f, lods[0].screenRelativeTransitionHeight);
            Assert.AreEqual(1, lods[0].renderers.Length);
        }

        [Test]
        public void ATreeModelWithALODGroup_IsUsedAsItIs()
        {
            const string copyPath = Folder + "/Stone_Scenery.prefab";
            var model = SaveModel(withLods: true);

            Assert.AreSame(model, MapTerrainWriter.TreeModel(model, copyPath));
            Assert.IsFalse(File.Exists(copyPath));
        }

        /// <summary>Sides 1 and 3, each starting with an HQ at an offset and turn.</summary>
        private (MapPalette Palette, GameObject Prefab) Palette()
        {
            var prefab = Track(new GameObject("Headquarters"));
            var palette = MapFixture.Item<MapPalette>("Palette");
            palette.entries.Add(MapFixture.Item<MapProp>("HQ", i => { i.prefab = prefab; }));
            foreach (var id in new[] { 1, 3 })
            {
                var side = new MapPaletteSide { side = id };
                side.startKit.Add(new MapStartItem { key = "HQ", offset = new Vector2(1f, 2f), yaw = 45f });
                palette.sides.Add(side);
            }

            return (palette, prefab);
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

        private void SavePrefab(string path)
        {
            var root = Track(new GameObject(Path.GetFileNameWithoutExtension(path)));
            new GameObject("Child").transform.SetParent(root.transform, false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private GameObject SaveModel(bool withLods)
        {
            var root = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            if (withLods)
            {
                root.AddComponent<LODGroup>();
            }

            return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Stone.prefab");
        }
    }
}
