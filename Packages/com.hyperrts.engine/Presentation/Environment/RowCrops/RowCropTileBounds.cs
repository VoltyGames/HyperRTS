using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>Each crop tile's box, from the ground under it to the tallest plant, for culling.</summary>
    internal static class RowCropTileBounds
    {
        public static Bounds[] Measure(RowCropFields fields, Terrain terrain)
        {
            var height = 0f;
            fields.crops.ForEach(crop => height = Mathf.Max(height, crop.height * Mathf.Max(1f, crop.growthVariation.y)));
            var bounds = new Bounds[fields.tiles.Count];
            for (var t = 0; t < bounds.Length; t++)
            {
                var rect = fields.TileRect(fields.tiles[t]);
                var (low, high) = GroundRange(rect, terrain);
                bounds[t] = RowCropView.TileBounds(rect, low, high, height);
            }

            return bounds;
        }

        /// <summary>Lowest and highest ground over a tile, from a 3 by 3 grid of samples.</summary>
        public static (float Low, float High) GroundRange(Rect rect, Terrain terrain)
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
