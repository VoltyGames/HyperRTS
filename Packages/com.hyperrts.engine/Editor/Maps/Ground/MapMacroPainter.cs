using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>Writes the terrain's macro map (<see cref="MapMacro"/>) as a linear texture over the terrain.</summary>
    public static class MapMacroPainter
    {
        private const float PixelsPerMetre = 0.8f;

        public static Texture2D Write(string path, MapLayout layout, MapLand land, MapShapes shapes, MapHeights heights,
            MapClimate climate)
        {
            var area = layout.TerrainBounds;
            var size = Mathf.NextPowerOfTwo(Mathf.CeilToInt(area.width * PixelsPerMetre));
            var macro = new MapMacro(layout, land, shapes, heights, climate);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = area.min + new Vector2((x + 0.5f) / size * area.width, (y + 0.5f) / size * area.height);
                    pixels[y * size + x] = macro.At(p);
                }
            }

            return EditorTextures.WritePng(path, size, size, TextureFormat.RGBA32, pixels, importer =>
            {
                importer.sRGBTexture = false;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            });
        }
    }
}
