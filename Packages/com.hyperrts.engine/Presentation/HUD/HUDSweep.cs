using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD;

/// <summary>
/// A clockwise progress sweep from twelve o'clock: the part still to do is shaded in <c>--sweep-color</c>, so the
/// picture under it uncovers as the work completes.
/// </summary>
[UxmlElement]
public partial class HUDSweep : HUDPaintedElement
{
    public HUDSweep() : base("--sweep-color")
    {
    }

    /// <summary>0 to 1; repaints only when it changes.</summary>
    public float Fraction
    {
        get;
        set
        {
            var fraction = Mathf.Clamp01(value);
            if (Mathf.Approximately(fraction, field))
            {
                return;
            }

            field = fraction;
            MarkDirtyRepaint();
        }
    }

    protected override void Paint(MeshGenerationContext context)
    {
        var rect = contentRect;
        if (Fraction >= 1f || rect.width <= 0f)
        {
            return;
        }

        // The radius reaches the corners; the button's overflow clips the disc to a square.
        var centre = rect.center;
        var radius = rect.size.magnitude * 0.5f;
        var painter = context.painter2D;
        painter.fillColor = PaintColor;
        painter.BeginPath();
        painter.MoveTo(centre);
        painter.Arc(centre, radius, Angle.Degrees(-90f + 360f * Fraction), Angle.Degrees(270f));
        painter.ClosePath();
        painter.Fill();
    }
}
