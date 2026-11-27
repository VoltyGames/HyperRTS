using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground;

/// <summary>
/// Where a point sits on a land-use border, after the edge wander: the region it falls in, the region across the
/// nearest border, its signed distance into its own region and the band the two blend over.
/// </summary>
/// <param name="Point">Where the land was looked up: the map point moved by the edge wander.</param>
/// <param name="Other">The region across the nearest border; <paramref name="Own"/> on land of one region.</param>
/// <param name="Inside">Metres into <paramref name="Own"/> from the border; slightly negative just over it.</param>
/// <param name="Width">Width of the blend band across the border (metres).</param>
public readonly record struct MapEdgeSample(Vector2 Point, int Own, int Other, float Inside, float Width)
{
    /// <summary>Share of <see cref="Own"/>: a half at the border, all of it half a band in.</summary>
    public float OwnShare => MapGeometry.Smooth(0.5f + Inside / Width);

    /// <summary>The region holding the point where the band crosses its middle, as grass and tint see it.</summary>
    public int Region => Inside >= 0f ? Own : Other;
}
