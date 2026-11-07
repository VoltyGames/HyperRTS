using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The line under the Map Editor canvas: cursor position in metres, zoom, grid size and the last preview's time.</summary>
    [UxmlElement]
    public sealed partial class MapStatusBar : VisualElement
    {
        private readonly Label _cursor = Item("Cursor in metres, east then north.");
        private readonly Label _zoom = Item("Pixels per metre, as a percentage of one.");
        private readonly Label _grid = Item("The distance between grid lines.");
        private readonly Label _preview = Item("When the preview last finished.");

        public MapStatusBar()
        {
            AddToClassList("hrts-map-status");
            Add(_cursor);
            Add(_zoom);
            Add(_grid);
            _preview.AddToClassList("hrts-fill");
            Add(_preview);
            ShowCursor(null);
            ShowPreviewed(null);
        }

        /// <summary>The pointer's map position in metres, or null once it has left the map.</summary>
        public void ShowCursor(Vector2? world) => _cursor.text = world.HasValue
            ? FormattableString.Invariant($"X {world.Value.x:0}  Z {world.Value.y:0} m")
            : "X -  Z -";

        public void ShowView(float pixelsPerMetre, float gridMetres)
        {
            _zoom.text = FormattableString.Invariant($"Zoom {pixelsPerMetre * 100f:0}%");
            _grid.text = FormattableString.Invariant($"Grid {gridMetres:0.#} m");
        }

        public void ShowPreviewed(DateTime? at) =>
            _preview.text = at.HasValue ? $"Preview {at.Value:HH:mm:ss}" : "No preview yet";

        private static Label Item(string tooltip)
        {
            var label = new Label { tooltip = tooltip };
            label.AddToClassList("hrts-map-status__item");
            return label;
        }
    }
}
