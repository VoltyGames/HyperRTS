using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement
{
    /// <summary>
    /// Where generated objects may go: off roads, water, ravines and bases, and apart from each other. Reserved
    /// circles sit in a spatial hash, since villages and belts reserve thousands.
    /// </summary>
    public sealed class MapClearance
    {
        /// <summary>Metres an off-road object keeps clear of a road's edge.</summary>
        public const float RoadMargin = 1f;

        private const float EdgeMargin = 4f;
        private const float Cell = 8f;
        private readonly MapLayout _layout;
        private readonly MapShapes _shapes;
        private readonly float _baseRadius;
        private readonly Dictionary<Vector2Int, List<(Vector2 Position, float Radius)>> _cells = new();

        public MapClearance(MapLayout layout, MapShapes shapes, float baseRadius)
        {
            _layout = layout;
            _shapes = shapes;
            _baseRadius = baseRadius;
        }

        /// <summary>Clear of bases, roads, rivers, ravines and the map edge (not of other objects).</summary>
        public bool IsOpenGround(Vector2 point, float radius) => IsOffRoad(point, radius) && !_shapes.InRavine(point);

        /// <summary>Clear of bases, roads, rivers and the map edge; ravine floors are fine (boulders, wrecks).</summary>
        public bool IsOffRoad(Vector2 point, float radius) =>
            IsDry(point, radius) && _shapes.RoadEdgeDistance(point) > radius + RoadMargin;

        /// <summary>Clear of bases, rivers and the map edge; roadsides and ravines are fine (pylons, fences).</summary>
        public bool IsDry(Vector2 point, float radius)
        {
            var inner = _layout.Bounds;
            inner.min += Vector2.one * (radius + EdgeMargin);
            inner.max -= Vector2.one * (radius + EdgeMargin);
            if (!inner.Contains(point) || InBase(point, radius))
            {
                return false;
            }

            return _shapes.RiverEdgeDistance(point) > radius + 2f;
        }

        public bool InBase(Vector2 point, float radius)
        {
            foreach (var spawn in _layout.Spawns)
            {
                if ((spawn.Position - point).magnitude < _baseRadius + radius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when nothing reserved overlaps a circle.</summary>
        public bool IsFree(Vector2 point, float radius)
        {
            var min = Key(point - Vector2.one * radius);
            var max = Key(point + Vector2.one * radius);
            for (var z = min.y; z <= max.y; z++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (!_cells.TryGetValue(new Vector2Int(x, z), out var list))
                    {
                        continue;
                    }

                    foreach (var (position, taken) in list)
                    {
                        if ((position - point).magnitude < radius + taken)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>Distance to the nearest reserved circle's edge, or infinity beyond <paramref name="maxDistance"/>.</summary>
        public float ReservedEdgeDistance(Vector2 point, float maxDistance)
        {
            // Inline cell loop: this runs per detail cell, so an iterator would allocate millions of enumerators.
            var best = float.PositiveInfinity;
            var min = Key(point - Vector2.one * maxDistance);
            var max = Key(point + Vector2.one * maxDistance);
            for (var z = min.y; z <= max.y; z++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (!_cells.TryGetValue(new Vector2Int(x, z), out var list))
                    {
                        continue;
                    }

                    foreach (var (position, taken) in list)
                    {
                        best = Mathf.Min(best, (position - point).magnitude - taken);
                    }
                }
            }

            return best <= maxDistance ? best : float.PositiveInfinity;
        }

        /// <summary>Reserves a circle in every hash cell it overlaps, so a query only looks at its own cells.</summary>
        public void Reserve(Vector2 point, float radius)
        {
            var min = Key(point - Vector2.one * radius);
            var max = Key(point + Vector2.one * radius);
            for (var z = min.y; z <= max.y; z++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var key = new Vector2Int(x, z);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        _cells[key] = list = new List<(Vector2, float)>();
                    }

                    list.Add((point, radius));
                }
            }
        }

        private static Vector2Int Key(Vector2 point) => Vector2Int.FloorToInt(point / Cell);
    }
}
