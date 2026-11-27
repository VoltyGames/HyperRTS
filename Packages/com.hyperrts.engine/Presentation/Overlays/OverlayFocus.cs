using HyperRTS.Presentation.Overlays.Bars;

namespace HyperRTS.Presentation.Overlays;

/// <summary>What the local player is doing with one unit this frame, which decides its marker and health bar.</summary>
public readonly struct OverlayFocus(bool selected, bool hovered, bool damaged)
{
    public readonly bool Selected = selected;
    public readonly bool Hovered = hovered;
    public readonly bool Damaged = damaged;

    public bool ShowsMarker => Selected || Hovered;

    public bool ShowsBar(HealthBarVisibility visibility)
    {
        if (ShowsMarker)
        {
            return true;
        }

        return visibility switch
        {
            HealthBarVisibility.Always => true,
            HealthBarVisibility.DamagedAndSelected => Damaged,
            _ => false,
        };
    }
}
