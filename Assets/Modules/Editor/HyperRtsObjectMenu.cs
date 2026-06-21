using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor
{
    /// <summary>GameObject ▸ HyperRTS menu items that drop the engine rig prefabs into the scene.</summary>
    internal static class HyperRtsObjectMenu
    {
        [MenuItem("GameObject/HyperRTS/RTS World", false, 10)]
        private static void CreateRtsWorld(MenuCommand cmd) =>
            Place("Assets/Modules/Prefabs/RTSWorld.prefab", "Create RTS World", cmd);

        [MenuItem("GameObject/HyperRTS/Selection UI", false, 10)]
        private static void CreateSelectionUi(MenuCommand cmd) =>
            Place("Assets/Modules/Prefabs/UI/SelectionUI.prefab", "Create Selection UI", cmd);

        private static void Place(string prefabPath, string undoLabel, MenuCommand cmd)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"HyperRTS: prefab not found at '{prefabPath}'.");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, undoLabel);
            UnityEditor.Selection.activeGameObject = go;
        }
    }
}
