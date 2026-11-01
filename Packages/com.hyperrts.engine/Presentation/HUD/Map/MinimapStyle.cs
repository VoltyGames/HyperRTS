using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Map
{
    /// <summary>The minimap's colours, line widths and marker radii, read from the custom properties in HUD.uss.</summary>
    internal sealed class MinimapStyle
    {
        private readonly CustomStyleProperty<Color> _viewColor = new("--minimap-view-color");
        private readonly CustomStyleProperty<float> _viewWidth = new("--minimap-view-width");
        private readonly CustomStyleProperty<Color> _outlineColor = new("--minimap-outline-color");
        private readonly CustomStyleProperty<float> _outlineWidth = new("--minimap-outline-width");
        private readonly CustomStyleProperty<float> _unitRadius = new("--minimap-unit-radius");
        private readonly CustomStyleProperty<float> _structureRadius = new("--minimap-structure-radius");
        private readonly CustomStyleProperty<float> _nodeRadius = new("--minimap-node-radius");

        public MinimapStyle(VisualElement viewLayer, VisualElement markerLayer)
        {
            viewLayer.RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                evt.customStyle.TryGetValue(_viewColor, out var color);
                evt.customStyle.TryGetValue(_viewWidth, out var width);
                ViewColor = color;
                ViewWidth = width;
                viewLayer.MarkDirtyRepaint();
            });
            markerLayer.RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                evt.customStyle.TryGetValue(_outlineColor, out var color);
                evt.customStyle.TryGetValue(_outlineWidth, out var width);
                evt.customStyle.TryGetValue(_unitRadius, out var unit);
                evt.customStyle.TryGetValue(_structureRadius, out var structure);
                evt.customStyle.TryGetValue(_nodeRadius, out var node);
                OutlineColor = color;
                OutlineWidth = width;
                UnitRadius = unit;
                StructureRadius = structure;
                NodeRadius = node;
                markerLayer.MarkDirtyRepaint();
            });
        }

        public Color ViewColor { get; private set; }
        public float ViewWidth { get; private set; }
        public Color OutlineColor { get; private set; }
        public float OutlineWidth { get; private set; }
        public float UnitRadius { get; private set; }
        public float StructureRadius { get; private set; }
        public float NodeRadius { get; private set; }
    }
}
