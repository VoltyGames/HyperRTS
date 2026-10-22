using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// A clockwise progress sweep from twelve o'clock: the part still to do is shaded in <c>--sweep-color</c>, so the
    /// picture under it uncovers as the work completes.
    /// </summary>
    [UxmlElement]
    public partial class HUDSweep : VisualElement
    {
        private static readonly CustomStyleProperty<Color> ColorProperty = new("--sweep-color");

        private Color _color = new(0f, 0f, 0f, 0.6f);
        private float _fraction;

        public HUDSweep()
        {
            AddToClassList("hud-sweep");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        /// <summary>0 to 1; repaints only when it changes.</summary>
        public float Fraction
        {
            get => _fraction;
            set
            {
                var fraction = Mathf.Clamp01(value);
                if (Mathf.Approximately(fraction, _fraction))
                {
                    return;
                }

                _fraction = fraction;
                MarkDirtyRepaint();
            }
        }

        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(ColorProperty, out var color))
            {
                _color = color;
                MarkDirtyRepaint();
            }
        }

        private void Paint(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (_fraction >= 1f || rect.width <= 0f)
            {
                return;
            }

            // The radius reaches the corners; the button's overflow clips the disc to a square.
            var centre = rect.center;
            var radius = rect.size.magnitude * 0.5f;
            var painter = context.painter2D;
            painter.fillColor = _color;
            painter.BeginPath();
            painter.MoveTo(centre);
            painter.Arc(centre, radius, Angle.Degrees(-90f + 360f * _fraction), Angle.Degrees(270f));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
