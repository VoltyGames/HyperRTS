using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor
{
    /// <summary>GameObject ▸ HyperRTS menu: ready-to-bake templates and the engine rig prefabs.</summary>
    internal static class HyperRTSObjectMenu
    {
        private const string Menu = "GameObject/HyperRTS/";

        [MenuItem(Menu + "RTS World (Camera + HUD)", false, 10)]
        private static void CreateRTSWorld(MenuCommand cmd) =>
            Place("Assets/Modules/Prefabs/RTSWorld.prefab", "Create RTS World", cmd);

        [MenuItem(Menu + "Match", false, 30)]
        private static void CreateMatch(MenuCommand cmd) =>
            Finish(new GameObject("Match", typeof(MatchAuthoring)), "Create Match", cmd);

        [MenuItem(Menu + "Unit", false, 31)]
        private static void CreateUnit(MenuCommand cmd)
        {
            var go = Template("Unit", PrimitiveType.Capsule, new Vector3(0.8f, 0.9f, 0.8f), 0.9f);
            go.AddComponent<UnitAuthoring>().displayName = "Unit";
            Finish(go, "Create Unit", cmd);
        }

        [MenuItem(Menu + "Building", false, 32)]
        private static void CreateBuilding(MenuCommand cmd)
        {
            var go = Template("Building", PrimitiveType.Cube, new Vector3(4f, 2.5f, 4f), 1.25f);
            go.AddComponent<BuildingAuthoring>().displayName = "Building";
            Finish(go, "Create Building", cmd);
        }

        [MenuItem(Menu + "Resource Node", false, 33)]
        private static void CreateResourceNode(MenuCommand cmd)
        {
            var go = Template("Resource Node", PrimitiveType.Cylinder, new Vector3(2.4f, 0.6f, 2.4f), 0.6f);
            go.AddComponent<ResourceNodeAuthoring>();
            go.AddComponent<NavObstacleAuthoring>().size = new Vector2(2f, 2f);
            Finish(go, "Create Resource Node", cmd);
        }

        [MenuItem(Menu + "Nav Obstacle", false, 34)]
        private static void CreateObstacle(MenuCommand cmd)
        {
            var go = Template("Obstacle", PrimitiveType.Cube, new Vector3(4f, 2f, 4f), 1f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.AddComponent<NavObstacleAuthoring>().size = new Vector2(4f, 4f);
            Finish(go, "Create Nav Obstacle", cmd);
        }

        // Root keeps uniform scale for clean transforms; the visible model is a scaled child.
        private static GameObject Template(string name, PrimitiveType shape, Vector3 size, float centerY)
        {
            var root = new GameObject(name);
            var model = GameObject.CreatePrimitive(shape);
            model.name = "Model";
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = new Vector3(0f, centerY, 0f);
            model.transform.localScale = size;

            var collider = root.AddComponent<BoxCollider>();
            collider.center = model.transform.localPosition;
            collider.size = shape == PrimitiveType.Capsule ? new Vector3(size.x, size.y * 2f, size.z) : size;
            return root;
        }

        private static void Place(string prefabPath, string undoLabel, MenuCommand cmd)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"HyperRTS: prefab not found at '{prefabPath}'.");
                return;
            }

            Finish((GameObject)PrefabUtility.InstantiatePrefab(prefab), undoLabel, cmd);
        }

        private static void Finish(GameObject go, string undoLabel, MenuCommand cmd)
        {
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, undoLabel);
            UnityEditor.Selection.activeGameObject = go;
        }
    }
}
