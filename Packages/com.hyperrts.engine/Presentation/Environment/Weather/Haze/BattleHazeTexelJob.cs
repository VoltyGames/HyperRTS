using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Weather.Haze;

/// <summary>Writes one <see cref="BattleHaze"/> texel per run: one plus the haze, plus the mist where its layer lies low over the ground.</summary>
[BurstCompile]
public struct BattleHazeTexelJob : IJobParallelFor
{
    private const int Cells = BattleHaze.Cells;
    private const float MaxHalf = 60000f;

    [ReadOnly] public NativeArray<float> Amounts;
    [ReadOnly] public NativeArray<float> Ground;
    [ReadOnly] public NativeArray<float> Pooling;

    [WriteOnly] public NativeArray<half> Texels;

    public int2 Roll;
    public int Layers;
    public float Side;

    /// <summary>Mist extinction per metre at full pooling.</summary>
    public float MistExtinction;

    public FogShape Fog;

    public void Execute(int index)
    {
        var x = index % Cells;
        var row = index / Cells;
        var layer = row % Layers;
        var z = row / Layers;
        var cell = Wrap(x - Roll.x) + Wrap(z - Roll.y) * Cells;

        // The texture repeats every side up the y axis, so its top layers are the ground below world zero.
        var height = (layer + 0.5f) * Side / Layers;
        height -= height >= Side * 0.5f ? Side : 0f;
        var above = height - Ground[cell];
        var extinction = Pooling[cell] * MistExtinction * math.saturate(1f - above / BattleHaze.MistDepth);
        // The fog thins with height itself; dividing it out keeps the mist at its own extinction on any ground.
        var mist = extinction > 0f ? extinction / (Fog.Density * Fog.Multiplier(height)) : 0f;
        Texels[index] = (half)math.min(1f + Amounts[cell] + mist, MaxHalf);
    }

    private static int Wrap(int cell) => (cell % Cells + Cells) % Cells;
}
