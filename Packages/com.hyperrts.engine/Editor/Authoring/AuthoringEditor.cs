using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using System.Reflection;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Icons;
using HyperRTS.Editor.Validation;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

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
        private const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const long ChangeDelayMilliseconds = 100;

        private IssueBadge _issues;
        private FieldIssueMarks _marks;
        private HelpBox _buttonError;
        private IVisualElementScheduledItem _change;

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
            BuildButtons(root.Require<VisualElement>("buttons"));
            _buttonError = root.Require<HelpBox>("button-error");
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
                    issues.AddRange(ProjectValidator.Check(component));
                }
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
                var found = declaring.GetField(field, Methods | BindingFlags.DeclaredOnly);
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

        private void BuildButtons(VisualElement buttons)
        {
            foreach (var (label, glyph, method) in Buttons(target.GetType()))
            {
                buttons.Add(CreateButton(label, glyph, () => Press(label, method)));
            }

            buttons.SetHidden(buttons.childCount == 0);
        }

        private static Button CreateButton(string label, string glyph, Action click)
        {
            var button = new Button(click);
            button.AddToClassList("hrts-glyph-button");
            if (glyph.Length > 0)
            {
                button.Add(GlyphElements.CreateGlyph(glyph));
            }

            button.Add(new Label(label));
            return button;
        }

        /// <summary>The component's own [InspectorButton] methods, then the static editor ones that take it.</summary>
        private static IEnumerable<(string Label, string Glyph, MethodInfo Method)> Buttons(Type type)
        {
            foreach (var method in type.GetMethods(Methods))
            {
                var button = method.GetCustomAttribute<InspectorButtonAttribute>();
                if (button == null)
                {
                    continue;
                }

                if (method.GetParameters().Length > 0)
                {
                    throw new InvalidOperationException($"[InspectorButton] {type.Name}.{method.Name} must take no parameters.");
                }

                yield return (button.Label, button.Glyph, method);
            }

            foreach (var method in TypeCache.GetMethodsWithAttribute<InspectorButtonAttribute>())
            {
                if (TakesComponent(method, type))
                {
                    var button = method.GetCustomAttribute<InspectorButtonAttribute>();
                    yield return (button.Label, button.Glyph, method);
                }
            }
        }

        private static bool TakesComponent(MethodInfo method, Type type)
        {
            if (!method.IsStatic)
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(type);
        }

        // A method that throws InvalidOperationException names what is missing, so the inspector shows it.
        private void Press(string label, MethodInfo method)
        {
            _buttonError.SetHidden(true);
            foreach (var selected in targets)
            {
                var component = (Component)selected;
                try
                {
                    EditorUndo.RecordHierarchy(component.gameObject, label, () => Call(method, component));
                }
                catch (TargetInvocationException failure) when (failure.InnerException is InvalidOperationException problem)
                {
                    _buttonError.text = problem.Message;
                    _buttonError.SetHidden(false);
                    return;
                }
            }
        }

        private static void Call(MethodInfo method, Component component)
        {
            if (method.IsStatic)
            {
                method.Invoke(null, new object[] { component });
                return;
            }

            method.Invoke(component, null);
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
