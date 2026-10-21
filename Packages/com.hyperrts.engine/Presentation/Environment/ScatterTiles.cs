using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>A scatter renderer's tiles as culling boxes, and the ones a camera sees within its draw distance.</summary>
    public sealed class ScatterTiles
    {
        /// <summary>Horizontal metres a plant can stand outside its tile: its jitter is under half a spacing.</summary>
        public const float Overhang = 1f;

        private readonly Bounds[] _bounds;
        private readonly Plane[] _planes = new Plane[6];

        /// <summary>Boxes for <paramref name="count"/> tiles, from the ground under each to the tallest plant over it.</summary>
        public ScatterTiles(int count, Func<int, Rect> tileRect, float plantHeight, Terrain terrain)
        {
            _bounds = new Bounds[count];
            for (var t = 0; t < count; t++)
            {
                var rect = tileRect(t);
                var (low, high) = GroundRange(rect, terrain);
                _bounds[t] = TileBounds(rect, low, high, plantHeight);
            }
        }

        public int Count => _bounds.Length;

        /// <summary>Indices of the tiles in a camera's view and draw distance, in tile order.</summary>
        public List<int> Visible { get; } = new();

        public Bounds this[int tile] => _bounds[tile];

        /// <summary>Refills <see cref="Visible"/> for a camera at <paramref name="position"/>.</summary>
        public void FindVisible(Camera camera, Vector3 position, float distance)
        {
            Visible.Clear();
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            for (var t = 0; t < _bounds.Length; t++)
            {
                var near = Distances(_bounds[t], position).Near;
                if (near < distance && GeometryUtility.TestPlanesAABB(_planes, _bounds[t]))
                {
                    Visible.Add(t);
                }
            }
        }

        /// <summary>A tile's box: its square widened by the overhang, from the lowest ground to the tallest plant.</summary>
        public static Bounds TileBounds(Rect tile, float groundMin, float groundMax, float plantHeight)
        {
            var min = new Vector3(tile.xMin - Overhang, groundMin, tile.yMin - Overhang);
            var max = new Vector3(tile.xMax + Overhang, groundMax + plantHeight, tile.yMax + Overhang);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        /// <summary>Distance from the camera to the nearest and the farthest point of a box.</summary>
        public static (float Near, float Far) Distances(Bounds bounds, Vector3 camera)
        {
            var near = Mathf.Sqrt(bounds.SqrDistance(camera));
            var reach = Vector3.Max(bounds.max - camera, camera - bounds.min);
            return (near, reach.magnitude);
        }

        /// <summary>Lowest and highest ground over a tile, from a 3 by 3 grid of samples.</summary>
        private static (float Low, float High) GroundRange(Rect rect, Terrain terrain)
        {
            var (low, high) = (float.MaxValue, float.MinValue);
            var baseHeight = terrain.GetPosition().y;
            for (var i = 0; i <= 2; i++)
            {
                for (var j = 0; j <= 2; j++)
                {
                    var point = new Vector3(rect.xMin + rect.width * i * 0.5f, 0f, rect.yMin + rect.height * j * 0.5f);
                    var ground = baseHeight + terrain.SampleHeight(point);
                    low = Mathf.Min(low, ground);
                    high = Mathf.Max(high, ground);
                }
            }

            return (low - 0.5f, high + 0.5f);
        }
    }
}
