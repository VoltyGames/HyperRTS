using System;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common.Authoring;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>
    /// One chip per authoring component on an object, with its icon and worst issue. A click shows only that
    /// component's inspector, or all of them again when it is already the only one; the last chip adds a component.
    /// </summary>
    [UxmlElement]
    public sealed partial class ModuleStrip : VisualElement
    {
        private readonly VisualElement _chips;

        public ModuleStrip()
        {
            EditorAssets.Layout(typeof(ModuleStrip)).CloneTree(this);
            _chips = this.Require<VisualElement>("chips");
        }

        /// <summary>Shows a chip per component; <paramref name="issuesFor"/> is the inspector's per-refresh check.</summary>
        public void Show(GameObject target, Func<Component, ValidationIssues> issuesFor)
        {
            _chips.Clear();
            var components = target.GetComponents<AuthoringBehaviour>();
            var alone = SoleExpanded(components);
            foreach (var component in components)
            {
                _chips.Add(CreateChip(components, component, component == alone, issuesFor(component)));
            }

            _chips.Add(CreateAddChip(target));
        }

        // The one component whose inspector is open while the others are folded, if there is such a component.
        private static AuthoringBehaviour SoleExpanded(AuthoringBehaviour[] components)
        {
            var expanded = components.Where(InternalEditorUtility.GetIsInspectorExpanded).ToList();
            return expanded.Count == 1 && components.Length > 1 ? expanded[0] : null;
        }

        private static Button CreateChip(AuthoringBehaviour[] all, AuthoringBehaviour component, bool isSole, ValidationIssues issues)
        {
            var chip = new Button(() => ShowOnly(all, component));
            chip.AddToClassList("hrts-chip");
            chip.EnableInClassList("hrts-chip--sole", isSole);
            chip.tooltip = ObjectNames.NicifyVariableName(component.GetType().Name);

            var icon = new Image { image = EditorGUIUtility.ObjectContent(component, component.GetType()).image };
            icon.AddToClassList("hrts-chip__icon");
            chip.Add(icon);

            var name = new Label(EntitySummary.ComponentName(component.GetType()));
            name.AddToClassList("hrts-chip__name");
            chip.Add(name);

            if (issues.Count > 0)
            {
                var dot = new VisualElement();
                dot.AddToClassList("hrts-chip__dot");
                dot.AddToClassList(issues.Worst == MessageType.Error ? "hrts-chip__dot--error" : "hrts-chip__dot--warning");
                chip.Add(dot);
            }

            return chip;
        }

        private static Button CreateAddChip(GameObject target)
        {
            var chip = new Button { text = "+", tooltip = "Add a component" };
            chip.AddToClassList("hrts-chip");
            chip.AddToClassList("hrts-chip--add");
            chip.clicked += () => UnityEditor.PopupWindow.Show(chip.worldBound, new AddComponentPopup(target));
            return chip;
        }

        private static void ShowOnly(AuthoringBehaviour[] all, AuthoringBehaviour clicked)
        {
            var showAll = SoleExpanded(all) == clicked;
            foreach (var component in all)
            {
                InternalEditorUtility.SetIsInspectorExpanded(component, showAll || component == clicked);
            }

            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
    }
}
