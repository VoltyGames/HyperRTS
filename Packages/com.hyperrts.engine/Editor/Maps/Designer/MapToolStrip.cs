using System;
using System.Collections.Generic;
using HyperRTS.Presentation.HUD;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor toolbar's tool group: an icon button each for Select, Point, Line and Area (V, P, L, A while
    /// the canvas has focus; Escape returns to Select). The active tool's options sit in <see cref="MapToolOptions"/>.
    /// </summary>
    [UxmlElement]
    public sealed partial class MapToolStrip : VisualElement
    {
        private const string ActiveClass = "hrts-map-tool--active";

        [NoAutoStaticsCleanup]
        private static readonly Dictionary<MapToolMode, string> Glyphs = new()
        {
            [MapToolMode.Select] = "mouse-pointer-2",
            [MapToolMode.Point] = "map-pin",
            [MapToolMode.Line] = "spline",
            [MapToolMode.Area] = "square-dashed",
        };

        private readonly Dictionary<MapToolMode, ToolbarButton> _buttons = new();
        private MapToolbox _toolbox;

        public MapToolStrip()
        {
            AddToClassList("hrts-map-tools");
            foreach (MapToolMode mode in Enum.GetValues(typeof(MapToolMode)))
            {
                var button = new ToolbarButton(() => _toolbox?.SetMode(mode)) { tooltip = MapToolOptions.Tip(mode) };
                var glyph = new VisualElement { pickingMode = PickingMode.Ignore };
                glyph.AddToClassList("hrts-glyph");
                glyph.AddToClassList("hrts-glyph--" + Glyphs[mode]);
                button.Add(glyph);
                _buttons[mode] = button;
                Add(button);
            }
        }

        /// <summary>Follows <paramref name="toolbox"/> to mark the active tool; the window binds it once.</summary>
        public void Bind(MapToolbox toolbox)
        {
            _toolbox = toolbox;
            _toolbox.Changed += Show;
            Show();
        }

        private void Show()
        {
            foreach (var (mode, button) in _buttons)
            {
                button.EnableInClassList(ActiveClass, mode == _toolbox.Mode);
            }
        }
    }
}
