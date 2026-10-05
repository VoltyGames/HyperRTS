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
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
            {
                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    if (!DrawProperty(property))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
            foreach (var issue in AuthoringChecks.For((Component)target))
            {
                IssueGUI.Draw(issue);
            }
        }

        /// <summary>Override to draw one property your own way; return false to use the default field.</summary>
        protected virtual bool DrawProperty(SerializedProperty property) => false;

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
