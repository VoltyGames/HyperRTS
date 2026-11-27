using HyperRTS.Simulation.Common.Geometry;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Destruction;

/// <summary>A building's <see cref="SectionHole"/> buffer, read per section.</summary>
public readonly struct SectionHoles
{
    private readonly DynamicBuffer<SectionHole> _holes;

    public SectionHoles(DynamicBuffer<SectionHole> holes) => _holes = holes;

    public DynamicBuffer<SectionHole> All => _holes;

    /// <summary>The number, among the section's holes in buffer order, of the one nearest <paramref name="local"/>, or -1 when it has none.</summary>
    public int Nearest(int section, float3 local)
    {
        var nearest = Closest.None;
        var number = 0;
        foreach (var hole in _holes)
        {
            if (hole.Section == section)
            {
                nearest.Consider(number++, Entity.Null, math.distancesq(hole.Center, local));
            }
        }

        return nearest.Index;
    }
}
