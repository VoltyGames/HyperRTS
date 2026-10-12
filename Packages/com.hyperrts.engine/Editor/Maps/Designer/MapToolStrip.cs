using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor toolbar's tool group: Select, Point, Line and Area toggles (V, P, L, A; Escape returns to
    /// Select) and, while a drawing tool is active, the feature kind it adds.
    /// </summary>
    public sealed class MapToolStrip : VisualElement
    {
        private static readonly Dictionary<MapToolMode, string> Tips = new()
        {
            [MapToolMode.Select] = MapSelectTool.Help,
            [MapToolMode.Point] = "Point (P): each click adds the picked point or circle feature, copying the last " +
                "one's fields. Shared ones snap to the symmetry axis or centre.",
            [MapToolMode.Line] = "Line (L): click to add vertices; Enter or double-click finishes (2+ points), " +
                "Escape cancels.",
            [MapToolMode.Area] = "Area (A): click to add vertices; Enter or double-click finishes (3+ points), " +
                "Escape cancels.",
        };

        private readonly MapToolbox _toolbox;
        private readonly Dictionary<MapToolMode, ToolbarToggle> _toggles = new();
        private readonly PopupField<MapFeatureKind> _kind;

        public MapToolStrip(MapToolbox toolbox)
        {
            _toolbox = toolbox;
            AddToClassList("hrts-map-tools");
            foreach (MapToolMode mode in Enum.GetValues(typeof(MapToolMode)))
            {
                var toggle = new ToolbarToggle { text = mode.ToString(), tooltip = Tips[mode] };
                toggle.RegisterValueChangedCallback(_ =>
                {
                    _toolbox.SetMode(mode);
                    Show();
                });
                _toggles[mode] = toggle;
                Add(toggle);
            }

            _kind = new PopupField<MapFeatureKind>(new List<MapFeatureKind>(), -1, Label, Label)
            {
                tooltip = "The feature kind the tool adds.",
            };
            _kind.AddToClassList("hrts-map-tools__kind");
            _kind.RegisterValueChangedCallback(evt => _toolbox.SetKind(evt.newValue));
            Add(_kind);

            RegisterCallback<AttachToPanelEvent>(_ => _toolbox.Changed += Show);
            RegisterCallback<DetachFromPanelEvent>(_ => _toolbox.Changed -= Show);
            Show();
        }

        private static string Label(MapFeatureKind kind) => kind == null ? string.Empty : kind.Label;

        /// <summary>Checks the active tool's toggle and lists the kinds it adds.</summary>
        private void Show()
        {
            foreach (var (mode, toggle) in _toggles)
            {
                toggle.SetValueWithoutNotify(mode == _toolbox.Mode);
            }

            var kinds = MapToolbox.KindsFor(_toolbox.Mode);
            _kind.style.display = kinds.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _kind.choices = new List<MapFeatureKind>(kinds);
            _kind.SetValueWithoutNotify(_toolbox.Kind);
        }
    }
}
