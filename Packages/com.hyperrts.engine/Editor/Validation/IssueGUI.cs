using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Draws an issue as a help box with its quick-fix button, if it has one.</summary>
    public static class IssueGUI
    {
        /// <summary>Returns true when the fix ran, so callers can refresh their issue list.</summary>
        public static bool Draw(ValidationIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(issue.Message, issue.Severity);
                if (issue.Fix == null || !GUILayout.Button(issue.FixLabel, GUILayout.Width(100), GUILayout.Height(38)))
                {
                    return false;
                }
            }

            issue.Fix();
            return true;
        }
    }
}
