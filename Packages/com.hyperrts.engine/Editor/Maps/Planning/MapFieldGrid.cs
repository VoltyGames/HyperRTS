using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// A farmland kit's field grid in the authored sector, turned to the rows' direction: u runs along the rows (field
    /// length), v across them. Grid lines pass through the map centre, so a sector edge on an axis is a field edge.
    /// </summary>
    public sealed class MapFieldGrid
    {
        private const int Offset = 1 << 12;

        public MapFieldGrid(MapFarmlandKit kit, float angle)
        {
            Forward = MapGeometry.Forward(angle);
            Right = new Vector2(Forward.y, -Forward.x);
            Length = Mathf.Max(20f, kit.fieldSize.x);
            Width = Mathf.Max(10f, kit.fieldSize.y);
        }

        public Vector2 Forward { get; }

        public Vector2 Right { get; }

        public float Length { get; }

        public float Width { get; }

        public Vector2 ToGrid(Vector2 point) => new(Vector2.Dot(point, Forward), Vector2.Dot(point, Right));

        public Vector2 FromGrid(float u, float v) => Forward * u + Right * v;

        /// <summary>A field's key: its column along the rows and row across them, packed.</summary>
        public int Field(Vector2 point)
        {
            var grid = ToGrid(point);
            var i = Mathf.FloorToInt(grid.x / Length) + Offset;
            var j = Mathf.FloorToInt(grid.y / Width) + Offset;
            return i * (Offset * 2) + j;
        }

        /// <summary>The u of the field-end line <paramref name="index"/>.</summary>
        public float EndLine(int index) => index * Length;

        /// <summary>The range of field-end line indices crossing a rectangle of the sector.</summary>
        public (int First, int Last) EndLines(Rect area)
        {
            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var corner in Corners(area))
            {
                var u = ToGrid(corner).x;
                min = Mathf.Min(min, u);
                max = Mathf.Max(max, u);
            }

            return (Mathf.CeilToInt(min / Length), Mathf.FloorToInt(max / Length));
        }

        /// <summary>The v range a rectangle of the sector covers.</summary>
        public Vector2 Across(Rect area)
        {
            var range = new Vector2(float.MaxValue, float.MinValue);
            foreach (var corner in Corners(area))
            {
                var v = ToGrid(corner).y;
                range = new Vector2(Mathf.Min(range.x, v), Mathf.Max(range.y, v));
            }

            return range;
        }

        private static Vector2[] Corners(Rect area) =>
            new[] { area.min, area.max, new Vector2(area.xMin, area.yMax), new Vector2(area.xMax, area.yMin) };
    }
}
