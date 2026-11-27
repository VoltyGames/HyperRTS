namespace HyperRTS.Presentation.Rendering.Silhouettes;

/// <summary>The rendering layer <see cref="RenderingLayerBakingSystem"/> puts units on for the silhouettes.</summary>
public static class UnitRenderingLayer
{
    public const int Index = 8;
    public const string Name = "Units";

    public const uint Mask = 1u << Index;
}
