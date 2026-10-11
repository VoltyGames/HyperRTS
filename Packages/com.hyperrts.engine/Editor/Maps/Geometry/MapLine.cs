using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>A placed polyline (river, road, ravine) in map space with its width and a bounds box for quick rejects.</summary>
    public sealed class MapLine
    {
        public MapLine(List<Vector2> points, float width, float reach)
        {
            Points = points;
            Width = width;
            Bounds = MapGeometry.Bounds(points, reach);
            Length = MapGeometry.Length(points);
        }

        public List<Vector2> Points { get; }

        public float Width { get; }

        public float Length { get; }

        /// <summary>The points' box grown by the line's reach: nothing outside it is affected by the line.</summary>
        public Rect Bounds { get; }

        /// <summary>Distance to the line, or infinity outside its reach.</summary>
        public float Distance(Vector2 point) =>
            Bounds.Contains(point) ? MapGeometry.DistanceToPolyline(Points, point) : float.PositiveInfinity;

        public float Distance(Vector2 point, out float along)
        {
            along = 0f;
            return Bounds.Contains(point)
                ? MapGeometry.DistanceToPolyline(Points, point, out along)
                : float.PositiveInfinity;
        }
    }
}
