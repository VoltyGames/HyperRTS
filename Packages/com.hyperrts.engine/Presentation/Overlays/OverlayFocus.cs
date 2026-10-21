namespace HyperRTS.Presentation.Overlays
{
    /// <summary>What the local player is doing with one unit this frame, which decides its marker and health bar.</summary>
    public readonly struct OverlayFocus
    {
        public readonly bool Selected;
        public readonly bool Hovered;
        public readonly bool Damaged;

        public OverlayFocus(bool selected, bool hovered, bool damaged)
        {
            Selected = selected;
            Hovered = hovered;
            Damaged = damaged;
        }

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
}
