namespace HyperRTS.Presentation.Rendering
{
    /// <summary>The rendering layer <see cref="UnitRenderingLayerBakingSystem"/> puts units on for the unit silhouettes.</summary>
    public static class UnitRenderingLayer
    {
        public const int Index = 8;
        public const string Name = "Units";

        public const uint Mask = 1u << Index;
    }
}
