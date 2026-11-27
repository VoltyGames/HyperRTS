using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning;

/// <summary>Keeps trees apart and off roads, water, steep ground, bases and placed objects.</summary>
public sealed class MapTreeSpacing(MapShapes shapes, MapHeights heights, MapClearance clearance)
{
    private const float Cell = 4f;
    private const float MaxSlope = 38f;
    private readonly Dictionary<Vector2Int, List<Vector2>> _cells = [];

    /// <summary>Whether a tree may grow here, at least <paramref name="spacing"/> metres from other trees.</summary>
    public bool Fits(Vector2 point, float spacing)
    {
        if (clearance.InBase(point, 2f) || !clearance.IsFree(point, 1.5f))
        {
            return false;
        }

        if (shapes.WaterEdgeDistance(point) < 1f || shapes.RoadEdgeDistance(point) < 1.5f)
        {
            return false;
        }

        if (heights.Slope(point) > MaxSlope || !heights.Bounds.Contains(point))
        {
            return false;
        }

        return !Crowded(point, spacing);
    }

    public void Add(Vector2 point)
    {
        var key = Key(point);
        if (!_cells.TryGetValue(key, out var list))
        {
            _cells[key] = list = [];
        }

        list.Add(point);
    }

    private bool Crowded(Vector2 point, float spacing)
    {
        var reach = Mathf.CeilToInt(spacing / Cell);
        var key = Key(point);
        for (var dz = -reach; dz <= reach; dz++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                if (!_cells.TryGetValue(key + new Vector2Int(dx, dz), out var list))
                {
                    continue;
                }

                foreach (var other in list)
                {
                    if ((other - point).sqrMagnitude < spacing * spacing)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static Vector2Int Key(Vector2 point) => Vector2Int.FloorToInt(point / Cell);
}
