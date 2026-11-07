using System.Collections.Generic;
using HyperRTS.Presentation.HUD;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The strip under the Map Editor toolbar: what the active tool does and, for a drawing tool, the feature kind it
    /// adds.
    /// </summary>
    [UxmlElement]
    public sealed partial class MapToolOptions : VisualElement
    {
        [NoAutoStaticsCleanup]
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

        private readonly PopupField<MapFeatureKind> _kind;
        private readonly Label _tip = new();
        private MapToolbox _toolbox;

        public MapToolOptions()
        {
            AddToClassList("hrts-map-options");
            _kind = new PopupField<MapFeatureKind>(new List<MapFeatureKind>(), -1, Label, Label)
            {
                tooltip = "The feature kind the tool adds.",
            };
            _kind.AddToClassList("hrts-map-options__kind");
            _kind.RegisterValueChangedCallback(evt => _toolbox?.SetKind(evt.newValue));
            _tip.AddToClassList("hrts-hint");
            Add(_kind);
            Add(_tip);
        }

        /// <summary>What the tool does and its shortcut, for its button's tooltip and this strip.</summary>
        public static string Tip(MapToolMode mode) => Tips[mode];

        /// <summary>Follows <paramref name="toolbox"/>; the window binds it once.</summary>
        public void Bind(MapToolbox toolbox)
        {
            _toolbox = toolbox;
            _toolbox.Changed += Show;
            Show();
        }

        private static string Label(MapFeatureKind kind) => kind == null ? string.Empty : kind.Label;

        private void Show()
        {
            var kinds = MapToolbox.KindsFor(_toolbox.Mode);
            _kind.SetHidden(kinds.Count == 0);
            _kind.choices = new List<MapFeatureKind>(kinds);
            _kind.SetValueWithoutNotify(_toolbox.Kind);
            _tip.text = Tip(_toolbox.Mode);
        }
    }
}
