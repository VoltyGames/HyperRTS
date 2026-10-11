using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement
{
    /// <summary>Where generated objects may go: off roads, water, ravines and bases, and apart from each other.</summary>
    public sealed class MapClearance
    {
        private const float EdgeMargin = 4f;
        private readonly MapLayout _layout;
        private readonly MapShapes _shapes;
        private readonly float _baseRadius;
        private readonly List<(Vector2 Position, float Radius)> _taken = new();

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
            IsDry(point, radius) && _shapes.RoadEdgeDistance(point) > radius + 1f;

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
            foreach (var (position, taken) in _taken)
            {
                if ((position - point).magnitude < radius + taken)
                {
                    return false;
                }
            }

            return true;
        }

        public void Reserve(Vector2 point, float radius) => _taken.Add((point, radius));

        /// <summary>Everything reserved so far, for passes (trees) that must keep off placed objects.</summary>
        public IReadOnlyList<(Vector2 Position, float Radius)> Taken => _taken;
    }
}
