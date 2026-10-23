using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>A click-through element that paints itself in a colour read from a USS custom property.</summary>
    public abstract class HUDPaintedElement : VisualElement
    {
        private readonly CustomStyleProperty<Color> _colorProperty;

        protected HUDPaintedElement(string colorVariable, Color fallback)
        {
            _colorProperty = new CustomStyleProperty<Color>(colorVariable);
            PaintColor = fallback;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        protected Color PaintColor { get; private set; }

        protected abstract void Paint(MeshGenerationContext context);

        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(_colorProperty, out var color))
            {
                PaintColor = color;
                MarkDirtyRepaint();
            }
        }
    }
}
