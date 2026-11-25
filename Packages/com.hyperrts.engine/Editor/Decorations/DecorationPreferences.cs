using System;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Decorations
{
    /// <summary>Preferences > HyperRTS: switches for the Hierarchy and Project window row decorations.</summary>
    internal static class DecorationPreferences
    {
        [NoAutoStaticsCleanup]
        private static readonly (string Label, string Field, Action Repaint)[] Toggles =
        {
            ("Hierarchy row icons", nameof(DecorationSettings.hierarchyRowIcons), HierarchyRows.RefreshAll),
            ("Project unit icons", nameof(DecorationSettings.projectUnitIcons), EditorApplication.RepaintProjectWindow),
            ("Project generator states", nameof(DecorationSettings.projectGeneratorStates), EditorApplication.RepaintProjectWindow),
        };

        [SettingsProvider]
        private static SettingsProvider Create() => new("Preferences/HyperRTS", SettingsScope.User)
        {
            label = "HyperRTS",
            keywords = Toggles.Select(toggle => toggle.Label),
            activateHandler = (_, root) =>
            {
                root.style.paddingLeft = 10;
                foreach (var (label, field, repaint) in Toggles)
                {
                    root.Add(CreateToggle(label, field, repaint));
                }

                DecorationSettings.Bind(root);
            },
        };

        private static Toggle CreateToggle(string label, string field, Action repaint)
        {
            var toggle = new Toggle(label) { bindingPath = field };
            toggle.RegisterValueChangedCallback(_ => repaint());
            return toggle;
        }
    }
}
