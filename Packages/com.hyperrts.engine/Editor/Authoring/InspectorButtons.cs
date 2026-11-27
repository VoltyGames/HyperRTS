using System;
using System.Collections.Generic;
using System.Reflection;
using HyperRTS.Editor.Authoring.ModelFill;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Icons;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Authoring;

/// <summary>Builds an inspector's <see cref="InspectorButtonAttribute"/> buttons and runs a press on every selected component.</summary>
internal sealed class InspectorButtons(Object[] targets, HelpBox error)
{
    public void Build(VisualElement buttons, Type type)
    {
        foreach (var (label, glyph, action) in Discover(type))
        {
            buttons.Add(CreateButton(label, glyph, () => Press(label, action)));
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

    /// <summary>The component's own [InspectorButton] methods, the static editor ones that take it, then its fill from model.</summary>
    private static IEnumerable<(string Label, string Glyph, Action<Component> Action)> Discover(Type type)
    {
        foreach (var method in type.GetMethods(AuthoringEditor.Members))
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

            yield return (button.Label, button.Glyph, component => Call(method, component));
        }

        foreach (var method in TypeCache.GetMethodsWithAttribute<InspectorButtonAttribute>())
        {
            if (TakesComponent(method, type))
            {
                var button = method.GetCustomAttribute<InspectorButtonAttribute>();
                yield return (button.Label, button.Glyph, component => Call(method, component));
            }
        }

        if (ModelFillCommands.CanFill(type))
        {
            yield return (ModelFillCommands.Label, "wand-sparkles", ModelFillCommands.Fill);
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
    private void Press(string label, Action<Component> action)
    {
        error.SetHidden(true);
        foreach (var selected in targets)
        {
            var component = (Component)selected;
            try
            {
                EditorUndo.RecordHierarchy(component.gameObject, label, () => action(component));
            }
            catch (InvalidOperationException problem)
            {
                error.text = problem.Message;
                error.SetHidden(false);
                return;
            }
        }
    }

    private static void Call(MethodInfo method, Component component)
    {
        try
        {
            method.Invoke(method.IsStatic ? null : component, method.IsStatic ? new object[] { component } : null);
        }
        catch (TargetInvocationException failure) when (failure.InnerException is InvalidOperationException problem)
        {
            throw problem;
        }
    }
}
