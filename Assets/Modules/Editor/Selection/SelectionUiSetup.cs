using HyperRTS.Presentation.Selection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Selection
{
    /// <summary>Creates a pre-wired Selection UI GameObject (PanelRenderer + SelectionDragBoxUI) from the GameObject menu.</summary>
    internal static class SelectionUiSetup
    {
        [MenuItem("GameObject/RTS/Selection UI", false, 10)]
        private static void CreateSelectionUi(MenuCommand menuCommand)
        {
            var go = new GameObject("Selection UI");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            var panelRenderer = go.AddComponent<PanelRenderer>();
            var panelSettings = FindFirstPanelSettings();
            if (panelSettings != null)
            {
                panelRenderer.panelSettings = panelSettings;
            }
            else
            {
                Debug.LogWarning("Selection UI: no PanelSettings asset found - assign one on the PanelRenderer.", go);
            }

            // RequireComponent on SelectionDragBoxUI is already satisfied by the PanelRenderer above.
            go.AddComponent<SelectionDragBoxUI>();

            Undo.RegisterCreatedObjectUndo(go, "Create Selection UI");
            UnityEditor.Selection.activeGameObject = go;
        }

        private static PanelSettings FindFirstPanelSettings()
        {
            var guids = AssetDatabase.FindAssets("t:PanelSettings");
            if (guids.Length == 0)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
        }
    }
}
