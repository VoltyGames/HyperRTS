namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// The rendering layer <see cref="RenderingLayerBakingSystem"/> puts buildings on for the team outlines. Apart from
    /// the units layer, so a building in front of a unit doesn't mark it as seen and hide its silhouette.
    /// </summary>
    public static class BuildingRenderingLayer
    {
        public const int Index = 9;
        public const string Name = "Buildings";

        public const uint Mask = 1u << Index;
    }
}
