using HyperRTS.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Default inspector plus the component's validation warnings; base of the HyperRTS inspectors.</summary>
    public abstract class AuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (var issue in AuthoringChecks.For((Component)target))
            {
                EditorGUILayout.HelpBox(issue.Message, issue.Severity);
            }
        }

        /// <summary>Records an undo step when a handle in the surrounding change check moved.</summary>
        protected bool Changed(string undoLabel)
        {
            if (!EditorGUI.EndChangeCheck())
            {
                return false;
            }

            Undo.RecordObject(target, undoLabel);
            return true;
        }
    }
}
