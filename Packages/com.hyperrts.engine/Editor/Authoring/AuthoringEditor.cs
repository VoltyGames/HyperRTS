using System.Collections.Generic;
using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Default inspector plus validation warnings, for every authoring component (engine or game).</summary>
    [CustomEditor(typeof(AuthoringBehaviour), true)]
    [CanEditMultipleObjects]
    public class AuthoringEditor : UnityEditor.Editor
    {
        private List<ValidationIssue> _issues = new();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            // Checks walk the hierarchy, so run them once per Layout and reuse them for the other events.
            if (Event.current.type == EventType.Layout)
            {
                _issues = AuthoringChecks.For((Component)target);
            }

            foreach (var issue in _issues)
            {
                IssueGUI.Draw(issue);
            }
        }
    }
}
