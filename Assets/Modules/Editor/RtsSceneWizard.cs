using HyperRTS.Core;
using HyperRTS.Simulation.Match;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor
{
    /// <summary>HyperRTS menu: one-click playable scene (rig + ground + SubScene with a Match) and docs links.</summary>
    internal static class RtsSceneWizard
    {
        private const string RigPath = "Assets/Modules/Prefabs/RTSWorld.prefab";

        [MenuItem("HyperRTS/Create RTS Scene...", false, 0)]
        private static void CreateScene()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create RTS Scene", "NewRTSScene", "unity",
                "Choose where to save the scene. Its SubScene is saved next to it.");
            if (!string.IsNullOrEmpty(path) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                CreateSceneAt(path);
            }
        }

        /// <summary>Builds and saves the scene at a project path such as <c>Assets/Scenes/Map.unity</c>.</summary>
        public static void CreateSceneAt(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLight();
            CreateGround(200f);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath), scene);
            EditorSceneManager.SaveScene(scene, path);

            var subScenePath = path.Replace(".unity", "_Entities.unity");
            CreateSubScene(subScenePath);
            var subScene = new GameObject("SubScene").AddComponent<SubScene>();
            subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
            subScene.AutoLoadScene = true;
            EditorSceneManager.SaveScene(scene);

            UnityEditor.Selection.activeGameObject = subScene.gameObject;
            Debug.Log("HyperRTS: scene created. Open the SubScene (tick its checkbox) and add units and buildings " +
                      "with GameObject ▸ HyperRTS. See " + HyperRTSDocs.GettingStarted);
        }

        [MenuItem("HyperRTS/Documentation/Getting Started", false, 100)]
        private static void OpenGettingStarted() => Help.BrowseURL(HyperRTSDocs.GettingStarted);

        [MenuItem("HyperRTS/Documentation/Module Reference", false, 101)]
        private static void OpenModules() => Help.BrowseURL(HyperRTSDocs.Modules);

        private static void CreateLight()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Unity's plane is 10 units wide, so scale it to the default map size.
        private static void CreateGround(float size)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
        }

        private static void CreateSubScene(string path)
        {
            var entities = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var match = new GameObject("Match", typeof(MatchAuthoring));
            EditorSceneManager.MoveGameObjectToScene(match, entities);
            EditorSceneManager.SaveScene(entities, path);
            EditorSceneManager.CloseScene(entities, true);
        }
    }
}
