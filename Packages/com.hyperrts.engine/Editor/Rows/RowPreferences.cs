using System;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Rows
{
    /// <summary>Preferences > HyperRTS: switches for the Hierarchy and Project window row decorations.</summary>
    internal static class RowPreferences
    {
        [SettingsProvider]
        private static SettingsProvider Create() => new("Preferences/HyperRTS", SettingsScope.User)
        {
            label = "HyperRTS",
            keywords = new[] { "Hierarchy row icons", "Project unit icons" },
            activateHandler = (_, root) =>
            {
                root.style.paddingLeft = 10;
                root.Add(CreateToggle("Hierarchy row icons", nameof(EditorState.hierarchyRowIcons), HierarchyRows.Refresh));
                root.Add(CreateToggle("Project unit icons", nameof(EditorState.projectUnitIcons), EditorApplication.RepaintProjectWindow));
                EditorState.Bind(root);
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
