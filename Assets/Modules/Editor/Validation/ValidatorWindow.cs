using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>HyperRTS ▸ Validate: lists setup problems in prefabs and open scenes; click one to select it.</summary>
    public class ValidatorWindow : EditorWindow
    {
        private List<ValidationIssue> _issues = new();
        private Vector2 _scroll;

        [MenuItem("HyperRTS/Validate", false, 20)]
        public static void Open() => GetWindow<ValidatorWindow>("HyperRTS Validator").Refresh();

        private void Refresh() => _issues = ProjectValidator.Run();

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    Refresh();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label($"{Count(MessageType.Error)} errors · {Count(MessageType.Warning)} warnings · " +
                                $"{Count(MessageType.Info)} notes", EditorStyles.miniLabel);
            }

            if (_issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No problems found. Closed SubScenes aren't checked: open them to include their " +
                                        "content.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var issue in _issues)
            {
                DrawIssue(issue);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawIssue(ValidationIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(issue.Message, issue.Severity);
                using (new EditorGUI.DisabledScope(issue.Context == null))
                {
                    var label = issue.Context != null ? issue.Context.name : "-";
                    if (GUILayout.Button(label, GUILayout.Width(140), GUILayout.Height(38)))
                    {
                        UnityEditor.Selection.activeObject = issue.Context;
                        EditorGUIUtility.PingObject(issue.Context);
                    }
                }
            }
        }

        private int Count(MessageType severity) => _issues.FindAll(issue => issue.Severity == severity).Count;
    }
}
