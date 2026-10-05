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

        private void Refresh()
        {
            _issues = ProjectValidator.Run();
            Repaint();
        }

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
                if (DrawIssue(issue))
                {
                    EditorApplication.delayCall += Refresh;
                    break;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>Returns true when a quick fix ran and the list is stale.</summary>
        private static bool DrawIssue(ValidationIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(issue.Context == null))
                {
                    var label = issue.Context != null ? issue.Context.name : "-";
                    if (GUILayout.Button(label, GUILayout.Width(140), GUILayout.Height(38)))
                    {
                        UnityEditor.Selection.activeObject = issue.Context;
                        EditorGUIUtility.PingObject(issue.Context);
                    }
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    return IssueGUI.Draw(issue);
                }
            }
        }

        private int Count(MessageType severity) => _issues.FindAll(issue => issue.Severity == severity).Count;
    }
}
