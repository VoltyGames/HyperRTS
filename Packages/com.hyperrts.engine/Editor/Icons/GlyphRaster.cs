using System.IO;
using System.Linq;
using Unity.VectorGraphics;
using UnityEngine;

namespace HyperRTS.Editor.Icons
{
    /// <summary>
    /// An SVG glyph filled into a square of pixels. The tessellated triangles are filled by hand on a finer grid, so
    /// no material or render target is needed.
    /// </summary>
    public sealed class GlyphRaster
    {
        // Samples per pixel along one axis, for the anti-aliased edge.
        private const int Samples = 4;

        private readonly int _grid;
        private readonly bool[] _covered;

        /// <summary>Fills the SVG into <paramref name="size"/> pixels square, scaled to <paramref name="glyphSize"/> and centred.</summary>
        public GlyphRaster(string svgPath, int size, float glyphSize)
        {
            _grid = size * Samples;
            _covered = new bool[_grid * _grid];

            var info = SVGParser.ImportSVG(new StringReader(File.ReadAllText(svgPath)), 96f, 1f, 0, 0, false);
            var options = new VectorUtils.TessellationOptions
            {
                StepDistance = 1f,
                MaxCordDeviation = 0.5f,
                MaxTanAngleDeviation = 0.1f,
                SamplingStepSize = 0.01f,
            };

            // A viewBox-only SVG reports no viewport, so the glyph is fitted to the shapes it draws.
            var geometries = VectorUtils.TessellateScene(info.Scene, options, info.NodeOpacity);
            var drawn = geometries.SelectMany(geometry => geometry.Vertices.Select(geometry.WorldTransform.MultiplyPoint)).ToArray();
            var min = new Vector2(drawn.Min(point => point.x), drawn.Min(point => point.y));
            var max = new Vector2(drawn.Max(point => point.x), drawn.Max(point => point.y));
            var scale = glyphSize * Samples / Mathf.Max(max.x - min.x, max.y - min.y);
            var offset = (new Vector2(size, size) * Samples - (max - min) * scale) / 2f;
            foreach (var geometry in geometries)
            {
                // Texture rows count up from the bottom, SVG rows down from the top.
                var points = geometry.Vertices.Select(vertex =>
                {
                    var local = (geometry.WorldTransform.MultiplyPoint(vertex) - min) * scale + offset;
                    return new Vector2(local.x, _grid - local.y);
                }).ToArray();
                for (var i = 0; i < geometry.Indices.Length; i += 3)
                {
                    Fill(points[geometry.Indices[i]], points[geometry.Indices[i + 1]], points[geometry.Indices[i + 2]]);
                }
            }
        }

        /// <summary>How much of the pixel the glyph covers, 0 to 1; row 0 is the bottom.</summary>
        public float Coverage(int x, int y)
        {
            var covered = 0;
            for (var sampleY = 0; sampleY < Samples; sampleY++)
            {
                for (var sampleX = 0; sampleX < Samples; sampleX++)
                {
                    covered += _covered[(y * Samples + sampleY) * _grid + x * Samples + sampleX] ? 1 : 0;
                }
            }

            return covered / (float)(Samples * Samples);
        }

        // Marks the samples whose centre lies in the triangle, whichever way it winds.
        private void Fill(Vector2 a, Vector2 b, Vector2 c)
        {
            var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)));
            var maxX = Mathf.Min(_grid - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x)));
            var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)));
            var maxY = Mathf.Min(_grid - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y)));
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    var side1 = Edge(a, b, point);
                    var side2 = Edge(b, c, point);
                    var side3 = Edge(c, a, point);
                    var anyNegative = side1 < 0f || side2 < 0f || side3 < 0f;
                    var anyPositive = side1 > 0f || side2 > 0f || side3 > 0f;
                    _covered[y * _grid + x] |= !(anyNegative && anyPositive);
                }
            }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }
}
