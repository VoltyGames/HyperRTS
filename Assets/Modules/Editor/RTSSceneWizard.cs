using System.IO;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor
{
    /// <summary>HyperRTS ▸ Create RTS Scene: a playable scene (rig, ground, SubScene with Match and bases) and docs links.</summary>
    public class RTSSceneWizard : ScriptableWizard
    {
        private static readonly Color[] PlayerColors =
        {
            new(0.2f, 0.45f, 1f), new(0.9f, 0.2f, 0.15f), new(0.25f, 0.8f, 0.3f), new(0.95f, 0.8f, 0.2f),
            new(0.6f, 0.3f, 0.9f), new(1f, 0.55f, 0.15f), new(0.2f, 0.8f, 0.8f), new(0.95f, 0.45f, 0.7f),
        };

        [Tooltip("Playable area (X by Z); the ground is sized to match.")]
        public Vector2 mapSize = new(200f, 200f);

        [Tooltip("Player 1 is you, the rest are AI on their own teams.")]
        [Range(1, 8)]
        public int players = 2;

        [Tooltip("Optional building prefab placed for every player around the map (a command centre).")]
        public GameEntityAuthoring startingBase;

        [MenuItem("HyperRTS/Create RTS Scene...", false, 0)]
        private static void Open() => DisplayWizard<RTSSceneWizard>("Create RTS Scene", "Create");

        [MenuItem("HyperRTS/Documentation/Getting Started", false, 100)]
        private static void OpenGettingStarted() => Help.BrowseURL(HyperRTSDocs.GettingStarted);

        [MenuItem("HyperRTS/Documentation/Module Reference", false, 101)]
        private static void OpenModules() => Help.BrowseURL(HyperRTSDocs.Modules);

        private void OnWizardCreate()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create RTS Scene", "NewRTSScene", "unity",
                "Choose where to save the scene. Its SubScene is saved next to it.");
            if (!string.IsNullOrEmpty(path) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                CreateSceneAt(path);
            }
        }

        /// <summary>Builds and saves the scene at a project path such as <c>Assets/Scenes/Map.unity</c>.</summary>
        public void CreateSceneAt(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLight();
            CreateGround();
            PrefabUtility.InstantiatePrefab(EditorAssets.RigPrefab, scene);
            EditorSceneManager.SaveScene(scene, path);

            var subScenePath = Path.ChangeExtension(path, null) + "_Entities.unity";
            CreateSubScene(subScenePath);
            var subScene = new GameObject("SubScene").AddComponent<SubScene>();
            subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
            subScene.AutoLoadScene = true;
            EditorSceneManager.SaveScene(scene);

            UnityEditor.Selection.activeGameObject = subScene.gameObject;
            Debug.Log("HyperRTS: scene created. Open the SubScene (tick its checkbox) and add units and buildings " +
                      "with GameObject ▸ HyperRTS. See " + HyperRTSDocs.GettingStarted);
        }

        private static void CreateLight()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Unity's plane is 10 units wide.
        private void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(mapSize.x / 10f, 1f, mapSize.y / 10f);
        }

        private void CreateSubScene(string path)
        {
            var entities = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var match = new GameObject("Match").AddComponent<MatchAuthoring>();
            match.mapSize = mapSize;
            match.players.Clear();
            for (var i = 0; i < players; i++)
            {
                match.players.Add(new PlayerSetup
                {
                    name = i == 0 ? "Player" : $"AI {i}",
                    team = i + 1,
                    color = PlayerColors[i % PlayerColors.Length],
                    control = i == 0 ? PlayerControl.LocalHuman : PlayerControl.AI,
                });
            }

            EditorSceneManager.MoveGameObjectToScene(match.gameObject, entities);
            PlaceBases(entities);
            EditorSceneManager.SaveScene(entities, path);
            EditorSceneManager.CloseScene(entities, true);
        }

        // Bases sit on a circle around the centre, player 1 at the bottom of the map.
        private void PlaceBases(Scene entities)
        {
            if (startingBase == null)
            {
                return;
            }

            var radius = Mathf.Min(mapSize.x, mapSize.y) * 0.35f;
            for (var i = 0; i < players; i++)
            {
                var angle = -Mathf.PI * 0.5f + i * 2f * Mathf.PI / players;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(startingBase.gameObject, entities);
                instance.transform.position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                var authoring = instance.GetComponent<GameEntityAuthoring>();
                authoring.owner = i + 1;
                PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);
            }
        }
    }
}
