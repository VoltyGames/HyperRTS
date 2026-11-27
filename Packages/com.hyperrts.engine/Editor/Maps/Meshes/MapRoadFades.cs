using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Meshes;

/// <summary>
/// Which road ends fade out in the mesh: free ends, and ends meeting a road of the same or higher rank. Ends that
/// carry on into another road or meet a lower rank stay solid.
/// </summary>
public static class MapRoadFades
{
    private const float Continues = 1.5f;

    /// <summary>Whether the road's start and end fade.</summary>
    public static (bool Start, bool End) EndFades(MapRoad road, IReadOnlyList<MapRoad> roads)
    {
        var points = road.Line.Points;
        return (Fades(road, points[0], roads), Fades(road, points[^1], roads));
    }

    /// <summary>
    /// The highest-ranked road that covers the end and outranks this one, or null when the end carries on or none
    /// does.
    /// </summary>
    public static MapRoad HigherAt(MapRoad road, Vector2 end, IReadOnlyList<MapRoad> roads)
    {
        if (!TryCovering(road, end, roads, out var covering))
        {
            return null;
        }

        MapRoad higher = null;
        foreach (var other in covering)
        {
            if (other.Class.rank > road.Class.rank && (higher == null || other.Class.rank > higher.Class.rank))
            {
                higher = other;
            }
        }

        return higher;
    }

    private static bool Fades(MapRoad road, Vector2 end, IReadOnlyList<MapRoad> roads)
    {
        if (!TryCovering(road, end, roads, out var covering))
        {
            return false;
        }

        return covering.Count == 0 || covering.Exists(other => road.Class.rank <= other.Class.rank);
    }

    // False when another road carries on at the end; otherwise the roads whose width covers it.
    private static bool TryCovering(MapRoad road, Vector2 end, IReadOnlyList<MapRoad> roads,
        out List<MapRoad> covering)
    {
        covering = new List<MapRoad>();
        foreach (var other in roads)
        {
            if (other == road)
            {
                continue;
            }

            var distance = other.Line.Distance(end, out var along);
            if (distance > other.Width * 0.5f)
            {
                continue;
            }

            var atEnd = along <= Continues || along >= other.Line.Length - Continues;
            if (atEnd && distance <= Continues)
            {
                return false;
            }

            covering.Add(other);
        }

        return true;
    }
}
