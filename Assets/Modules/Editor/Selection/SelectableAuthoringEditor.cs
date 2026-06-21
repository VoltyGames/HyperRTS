using HyperRTS.Simulation.Selection;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Selection
{
    /// <summary>Draws the default <see cref="SelectableAuthoring"/> inspector plus a live Selected/Base highlight preview.</summary>
    [CustomEditor(typeof(SelectableAuthoring))]
    [CanEditMultipleObjects]
    public class SelectableAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var authoring = (SelectableAuthoring)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Highlight Preview", EditorStyles.boldLabel);
            DrawSwatch("Selected", authoring.selectedColor);
            DrawSwatch("Base", authoring.baseColor);
        }

        private static void DrawSwatch(string label, Color color)
        {
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            var swatchRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y,
                rect.width - EditorGUIUtility.labelWidth, rect.height);

            EditorGUI.LabelField(labelRect, label);
            EditorGUI.DrawRect(swatchRect, color);
        }
    }
}
