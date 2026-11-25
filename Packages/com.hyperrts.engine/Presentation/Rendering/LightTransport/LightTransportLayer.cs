namespace HyperRTS.Presentation.Rendering.LightTransport
{
    /// <summary>The rendering layer <see cref="RenderingLayerBakingSystem"/> puts scene content on, and the one the Surface Cache volume is limited to.</summary>
    public static class LightTransportLayer
    {
        public const int Index = 9;
        public const string Name = "Light Transport";

        public const uint Mask = 1u << Index;
    }
}
