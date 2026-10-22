namespace HyperRTS.Presentation.HUD
{
    /// <summary>An element that fills the <see cref="HUDTooltip"/> while it is hovered or focused.</summary>
    public interface ITooltipSource
    {
        /// <summary>Changes whenever what <see cref="Describe"/> writes changes, so the tooltip refills.</summary>
        int TooltipVersion { get; }

        /// <summary>Fills the cleared <paramref name="tooltip"/>; false when there is nothing to show.</summary>
        bool Describe(HUDTooltip tooltip);
    }
}
