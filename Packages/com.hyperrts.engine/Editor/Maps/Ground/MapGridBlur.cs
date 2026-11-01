using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>Box blur of a map field with clamped borders, run across then along.</summary>
    public static class MapGridBlur
    {
        public static MapValues<float> Box(MapValues<float> field, int radius)
        {
            var grid = field.Grid;
            var across = new float[grid.Count];
            for (var z = 0; z < grid.Height; z++)
            {
                Line(field.Values, across, grid.Index(0, z), 1, grid.Width, radius);
            }

            var along = new MapValues<float>(grid);
            for (var x = 0; x < grid.Width; x++)
            {
                Line(across, along.Values, x, grid.Width, grid.Height, radius);
            }

            return along;
        }

        private static void Line(float[] source, float[] target, int start, int stride, int count, int radius)
        {
            var window = 2 * radius + 1;
            var sum = 0f;
            for (var k = -radius; k <= radius; k++)
            {
                sum += source[start + Mathf.Clamp(k, 0, count - 1) * stride];
            }

            for (var i = 0; i < count; i++)
            {
                target[start + i * stride] = sum / window;
                var leaving = Mathf.Clamp(i - radius, 0, count - 1);
                var entering = Mathf.Clamp(i + radius + 1, 0, count - 1);
                sum += source[start + entering * stride] - source[start + leaving * stride];
            }
        }
    }
}
