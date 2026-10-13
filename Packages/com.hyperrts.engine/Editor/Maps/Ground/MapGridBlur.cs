using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>Box blur of a row-major grid with clamped borders, run across then along.</summary>
    public static class MapGridBlur
    {
        public static float[] Box(float[] values, int width, int height, int radius)
        {
            var across = new float[values.Length];
            for (var z = 0; z < height; z++)
            {
                Line(values, across, z * width, 1, width, radius);
            }

            var along = new float[values.Length];
            for (var x = 0; x < width; x++)
            {
                Line(across, along, x, width, height, radius);
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
