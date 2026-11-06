using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>A click-through element that paints itself in a colour read from a USS custom property.</summary>
    public abstract class HUDPaintedElement : VisualElement
    {
        private readonly CustomStyleProperty<Color> _colorProperty;
        private readonly string _colorVariable;

        protected HUDPaintedElement(string colorVariable)
        {
            _colorVariable = colorVariable;
            _colorProperty = new CustomStyleProperty<Color>(colorVariable);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        protected Color PaintColor { get; private set; }

        protected abstract void Paint(MeshGenerationContext context);

        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (!evt.customStyle.TryGetValue(_colorProperty, out var color))
            {
                throw new System.InvalidOperationException(
                    $"HyperRTS: {GetType().Name} needs the USS custom property {_colorVariable} from the HUD theme.");
            }

            PaintColor = color;
            MarkDirtyRepaint();
        }
    }
}
