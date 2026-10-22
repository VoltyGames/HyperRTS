using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Validation;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Default inspector with a validation badge on top, for every authoring component (engine or game).</summary>
    [CustomEditor(typeof(AuthoringBehaviour), true)]
    [CanEditMultipleObjects]
    public class AuthoringEditor : UnityEditor.Editor
    {
        private IssueBadge _issues;

        public override VisualElement CreateInspectorGUI()
        {
            var root = EditorAssets.Layout(typeof(AuthoringEditor)).Instantiate();
            _issues = root.Require<IssueBadge>("issues");
            _issues.Fixed += OnChanged;
            BuildHeader(root.Require<VisualElement>("header"));
            InspectorElement.FillDefaultInspector(root.Require<VisualElement>("fields"), serializedObject, this);
            ConditionalFields.Bind(root, serializedObject);
            BuildFooter(root.Require<VisualElement>("footer"));

            // Rules also read sibling components and the hierarchy, so field edits alone don't cover every change.
            root.TrackSerializedObjectValue(serializedObject, _ => OnChanged());
            root.RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            root.RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            OnChanged();
            return root;
        }

        /// <summary>Adds elements to the header above the fields.</summary>
        protected virtual void BuildHeader(VisualElement header)
        {
        }

        /// <summary>Adds elements to the footer below the fields.</summary>
        protected virtual void BuildFooter(VisualElement footer)
        {
        }

        /// <summary>Re-reads what the header and footer show; runs after edits, undo and hierarchy changes.</summary>
        protected virtual void Refresh()
        {
        }

        private void OnChanged()
        {
            // Hierarchy events can arrive after the inspected objects were deleted.
            if (target == null)
            {
                return;
            }

            Refresh();
            _issues.Show(Validate());
        }

        // Every selected object, so a multi-selection shows each one's problems.
        private List<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            foreach (var selected in targets)
            {
                if (selected is Component component)
                {
                    issues.AddRange(ProjectValidator.Check(component));
                }
            }

            return issues;
        }

        private void Subscribe()
        {
            EditorApplication.hierarchyChanged += OnChanged;
            Undo.undoRedoPerformed += OnChanged;
        }

        private void Unsubscribe()
        {
            EditorApplication.hierarchyChanged -= OnChanged;
            Undo.undoRedoPerformed -= OnChanged;
        }
    }
}
