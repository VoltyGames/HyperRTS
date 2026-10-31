using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// RowCrops.compute's row lattice in C#: which spots can fall in a tile, and the hash channels the map tools share
    /// with the kernel. A plant belongs to the one tile its spot falls in (half-open).
    /// </summary>
    public static class RowCropSpots
    {
        // Match RowCrops.compute's CHANNEL_ defines.
        public const uint AcrossChannel = 1;
        public const uint AlongChannel = 2;

        /// <summary>Places a field's tramlines.</summary>
        public const uint TramlineChannel = 12;

        // Slack in lattice units so rounding never loses a spot.
        private const float Slack = 1e-4f;

        /// <summary>The job growing the spots of <paramref name="data"/>'s lattice that can fall in <paramref name="tile"/>.</summary>
        public static ScatterJob Job(Rect tile, int field, RowCropField data, float rowSpacing, float plantSpacing)
        {
            var (first, count) = Range(tile, data, rowSpacing, plantSpacing);
            return new ScatterJob(tile, field, first, count);
        }

        /// <summary>The first lattice indices (row, plant) whose spot can fall in <paramref name="rect"/>, and how many of each.</summary>
        public static (Vector2Int Min, Vector2Int Count) Range(Rect rect, RowCropField field, float rowSpacing,
            float plantSpacing)
        {
            var a = ToLattice(rect.min, field, rowSpacing, plantSpacing);
            var b = ToLattice(new Vector2(rect.xMax, rect.yMin), field, rowSpacing, plantSpacing);
            var c = ToLattice(new Vector2(rect.xMin, rect.yMax), field, rowSpacing, plantSpacing);
            var d = ToLattice(rect.max, field, rowSpacing, plantSpacing);
            var low = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
            var high = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
            var min = new Vector2Int(Mathf.CeilToInt(low.x - Slack), Mathf.CeilToInt(low.y - Slack));
            var max = new Vector2Int(Mathf.FloorToInt(high.x + Slack), Mathf.FloorToInt(high.y + Slack));
            return (min, Vector2Int.Max(Vector2Int.zero, max - min + Vector2Int.one));
        }

        /// <summary>The most plants one field can grow in a square tile (area plus a perimeter row), for buffer sizes.</summary>
        public static int PlantBound(float tileSize, float rowSpacing, float plantSpacing)
        {
            var area = tileSize * tileSize / (rowSpacing * plantSpacing);
            var edge = 4f * tileSize / Mathf.Min(rowSpacing, plantSpacing);
            return Mathf.CeilToInt(area + edge) + 1;
        }

        private static Vector2 ToLattice(Vector2 point, RowCropField field, float rowSpacing, float plantSpacing)
        {
            var offset = point - field.origin;
            return new Vector2(Vector2.Dot(offset, field.across) / rowSpacing, Vector2.Dot(offset, field.along) / plantSpacing);
        }
    }
}
