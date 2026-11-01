using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using System.Reflection;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Validation;
using HyperRTS.Presentation.HUD;
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
        private const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private IssueBadge _issues;
        private HelpBox _buttonError;

        public override VisualElement CreateInspectorGUI()
        {
            var root = EditorAssets.Layout(typeof(AuthoringEditor)).Instantiate();
            _issues = root.Require<IssueBadge>("issues");
            _issues.Fixed += OnChanged;
            BuildHeader(root.Require<VisualElement>("header"));
            InspectorElement.FillDefaultInspector(root.Require<VisualElement>("fields"), serializedObject, this);
            ConditionalFields.Bind(root, serializedObject);
            BuildButtons(root.Require<VisualElement>("buttons"));
            _buttonError = root.Require<HelpBox>("button-error");
            BuildFooter(root.Require<VisualElement>("footer"));

            // Rules also read sibling components and the hierarchy, so field edits alone don't cover every change.
            root.TrackSerializedObjectValue(serializedObject, _ => OnChanged());
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

        private void BuildButtons(VisualElement buttons)
        {
            foreach (var (label, method) in Buttons(target.GetType()))
            {
                buttons.Add(new Button(() => Press(label, method)) { text = label });
            }

            buttons.SetHidden(buttons.childCount == 0);
        }

        /// <summary>The component's own [InspectorButton] methods, then the static editor ones that take it.</summary>
        private static IEnumerable<(string Label, MethodInfo Method)> Buttons(Type type)
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

                yield return (button.Label, method);
            }

            foreach (var method in TypeCache.GetMethodsWithAttribute<InspectorButtonAttribute>())
            {
                if (TakesComponent(method, type))
                {
                    yield return (method.GetCustomAttribute<InspectorButtonAttribute>().Label, method);
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
