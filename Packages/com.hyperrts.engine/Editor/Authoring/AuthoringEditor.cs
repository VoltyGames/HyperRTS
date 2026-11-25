using System.Collections.Generic;
using System.Reflection;
using System;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common.Authoring;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>
    /// Inspector for every authoring component (engine or game): an issue strip on top, the fields with each
    /// <c>[Header]</c> as a foldout, unit suffixes, issue marks on the fields at fault, and the buttons.
    /// </summary>
    [CustomEditor(typeof(AuthoringBehaviour), true)]
    [CanEditMultipleObjects]
    public class AuthoringEditor : UnityEditor.Editor
    {
        internal const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const long ChangeDelayMilliseconds = 100;

        private IssueBadge _issues;
        private FieldIssueMarks _marks;
        private IVisualElementScheduledItem _change;
        private readonly Dictionary<Component, ValidationIssues> _checked = new();

        public override VisualElement CreateInspectorGUI()
        {
            var root = EditorAssets.Layout(typeof(AuthoringEditor)).Instantiate();
            _issues = root.Require<IssueBadge>("issues");
            _issues.Fixed += QueueChange;
            BuildHeader(root.Require<VisualElement>("header"));
            BuildFields(root.Require<VisualElement>("fields"));
            ConditionalFields.Bind(root, serializedObject);
            UnitSuffixes.Bind(root, target.GetType());
            _marks = new FieldIssueMarks(root);
            new InspectorButtons(targets, root.Require<HelpBox>("button-error")).Build(root.Require<VisualElement>("buttons"), target.GetType());
            BuildFooter(root.Require<VisualElement>("footer"));

            // Rules also read sibling components and the hierarchy, so field edits alone don't cover every change.
            _change = root.schedule.Execute(OnChanged);
            _change.Pause();
            root.TrackSerializedObjectValue(serializedObject, _ => QueueChange());
            root.WhileAttached(Subscribe, Unsubscribe);
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

        // One edit raises a value change, a hierarchy change and an undo record, and a drag raises them every frame,
        // so they share one refresh once they pause.
        private void QueueChange() => _change.ExecuteLater(ChangeDelayMilliseconds);

        private void OnChanged()
        {
            // Hierarchy events can arrive after the inspected objects were deleted.
            if (target == null)
            {
                return;
            }

            _checked.Clear();
            Refresh();
            var issues = Validate();
            _issues.Show(issues);
            _marks.Show(issues);
        }

        // Every selected object, so a multi-selection shows each one's problems.
        private List<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            foreach (var selected in targets)
            {
                if (selected is Component component)
                {
                    issues.AddRange(IssuesFor(component));
                }
            }

            return issues;
        }

        /// <summary>A component's issues, checked once per refresh however many parts of the inspector ask.</summary>
        protected ValidationIssues IssuesFor(Component component)
        {
            if (!_checked.TryGetValue(component, out var issues))
            {
                issues = ProjectValidator.Check(component);
                _checked[component] = issues;
            }

            return issues;
        }

        // Properties are laid out here rather than by FillDefaultInspector so a [Header] can start a foldout.
        private void BuildFields(VisualElement fields)
        {
            var container = fields;
            var type = target.GetType();
            var property = serializedObject.GetIterator();
            for (var enter = true; property.NextVisible(enter); enter = false)
            {
                if (property.propertyPath == "m_Script")
                {
                    continue;
                }

                var header = FindHeader(type, property.name);
                if (header != null)
                {
                    container = AddGroup(fields, $"{type.Name}/{header.header}", header.header);
                }

                container.Add(new PropertyField(property.Copy()));
            }
        }

        private static HeaderAttribute FindHeader(Type type, string field)
        {
            for (var declaring = type; declaring != null && declaring != typeof(MonoBehaviour); declaring = declaring.BaseType)
            {
                var found = declaring.GetField(field, Members | BindingFlags.DeclaredOnly);
                if (found != null)
                {
                    return found.GetCustomAttribute<HeaderAttribute>();
                }
            }

            return null;
        }

        private static Foldout AddGroup(VisualElement fields, string key, string title)
        {
            var group = new Foldout { text = title, value = !EditorState.instance.IsGroupClosed(key) };
            group.AddToClassList("hrts-group");
            // Foldouts inside the fields bubble their changes up, so only this one counts.
            group.RegisterValueChangedCallback(change =>
            {
                if (change.target == group)
                {
                    EditorState.instance.SetGroupClosed(key, !change.newValue);
                }
            });
            fields.Add(group);
            return group;
        }

        private void Subscribe()
        {
            EditorApplication.hierarchyChanged += QueueChange;
            Undo.undoRedoPerformed += QueueChange;
        }

        private void Unsubscribe()
        {
            EditorApplication.hierarchyChanged -= QueueChange;
            Undo.undoRedoPerformed -= QueueChange;
        }
    }
}
