using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Ground.Holes;

/// <summary>A terrain's hole texels: square, spanning its whole size from its south-west corner.</summary>
public readonly struct HoleGrid(float2 origin, float2 size, int resolution)
{
    public readonly float2 Origin = origin;
    public readonly float2 TexelSize = size / resolution;
    public readonly int Resolution = resolution;

    /// <summary>World x and z of the middle of texel (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public float2 TexelCentre(int x, int y) => Origin + (new float2(x, y) + 0.5f) * TexelSize;
}
