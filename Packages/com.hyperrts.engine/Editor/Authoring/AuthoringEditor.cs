using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Validation;
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
            var root = new VisualElement();
            EditorAssets.AddStyles(root);
            _issues = new IssueBadge(OnChanged);
            root.Add(_issues);
            BuildHeader(root);
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            ConditionalFields.Bind(root, serializedObject);
            BuildFooter(root);

            // Rules also read sibling components and the hierarchy, so field edits alone don't cover every change.
            root.TrackSerializedObjectValue(serializedObject, _ => OnChanged());
            root.RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            root.RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            OnChanged();
            return root;
        }

        /// <summary>Adds elements above the fields.</summary>
        protected virtual void BuildHeader(VisualElement root)
        {
        }

        /// <summary>Adds elements below the fields.</summary>
        protected virtual void BuildFooter(VisualElement root)
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
