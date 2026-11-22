using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Editor.Environment
{
    /// <summary>
    /// The water's tiling foam: R lace strands round bubble holes, G a flat-histogram clump field that foam thins
    /// through, B broad blobs for calm slicks and wind gusts.
    /// </summary>
    public static class FoamLace
    {
        private const int LargeCells = 9;
        private const int SmallCells = 23;
        private const float LargeStrand = 0.07f;
        private const float SmallStrand = 0.11f;

        /// <summary>Texels of a size × size tile, each channel 0..1.</summary>
        public static Vector3[] Texels(int size, uint seed)
        {
            var random = new Random(seed);
            var large = Strands(size, LargeCells, LargeStrand, ref random);
            var small = Strands(size, SmallCells, SmallStrand, ref random);
            var clumps = Mixed(TileableNoise.Lowpass(size, 5f, ref random), 0.75f, TileableNoise.Lowpass(size, 14f, ref random), 0.45f);
            TileableNoise.RankUniform(clumps);
            var blobs = TileableNoise.Lowpass(size, 3f, ref random);
            TileableNoise.RankUniform(blobs);

            var texels = new Vector3[size * size];
            for (var i = 0; i < texels.Length; i++)
            {
                texels[i] = new Vector3(Mathf.Max(large[i], small[i] * 0.85f), clumps[i], blobs[i]);
            }

            return texels;
        }

        // 1 on the borders between jittered cells, falling to 0 a strand's width (in cells) inside them: the walls
        // of the bubbles foam is made of.
        private static float[] Strands(int size, int cells, float width, ref Random random)
        {
            var points = new Vector2[cells * cells];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new Vector2(random.NextFloat(0.1f, 0.9f), random.NextFloat(0.1f, 0.9f));
            }

            var values = new float[size * size];
            var scale = (float)cells / size;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) * scale;
                    var (nearest, second) = TwoNearest(points, cells, p);
                    values[y * size + x] = 1f - Mathf.SmoothStep(0f, 1f, (second - nearest) / width);
                }
            }

            return values;
        }

        private static (float nearest, float second) TwoNearest(Vector2[] points, int cells, Vector2 p)
        {
            var cx = Mathf.FloorToInt(p.x);
            var cy = Mathf.FloorToInt(p.y);
            var nearest = float.MaxValue;
            var second = float.MaxValue;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var cell = new Vector2Int(cx + dx, cy + dy);
                    var point = points[Wrap(cell.y, cells) * cells + Wrap(cell.x, cells)] + cell;
                    var distance = Vector2.Distance(p, point);
                    if (distance < nearest)
                    {
                        second = nearest;
                        nearest = distance;
                    }
                    else if (distance < second)
                    {
                        second = distance;
                    }
                }
            }

            return (nearest, second);
        }

        private static float[] Mixed(float[] first, float firstWeight, float[] second, float secondWeight)
        {
            for (var i = 0; i < first.Length; i++)
            {
                first[i] = first[i] * firstWeight + second[i] * secondWeight;
            }

            return first;
        }

        private static int Wrap(int index, int size) => ((index % size) + size) % size;
    }
}
