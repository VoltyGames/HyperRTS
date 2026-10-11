using System.IO;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// The terrain's macro map: a gentle colour shift per field and per wood (no two fields ripen alike), broad
    /// noise over everything, and wetness near water and on ravine floors. RGB 0.5 is neutral; A is wetness.
    /// </summary>
    public static class MapMacroPainter
    {
        private const float PixelsPerMetre = 0.8f;
        private const float WetReach = 7f;

        public static Texture2D Write(string path, MapLayout layout, MapLand land, MapShapes shapes)
        {
            var area = layout.TerrainBounds;
            var size = Mathf.NextPowerOfTwo(Mathf.CeilToInt(area.width * PixelsPerMetre));
            var broad = new MapNoise(layout.Symmetry, layout.Spec.seed, 6);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = area.min + new Vector2((x + 0.5f) / size * area.width, (y + 0.5f) / size * area.height);
                    pixels[y * size + x] = Macro(land.At(p), shapes, broad, p);
                }
            }

            var image = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            image.SetPixels(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            return Import(path);
        }

        private static Color Macro(MapLandRegion region, MapShapes shapes, MapNoise broad, Vector2 p)
        {
            var light = 0.5f + 0.035f * broad.Sample(p, 90f);
            var warmth = 0f;
            if (region != null && Varies(region.Kind))
            {
                var random = new System.Random(region.Seed);
                light += ((float)random.NextDouble() - 0.5f) * 0.09f;
                warmth = ((float)random.NextDouble() - 0.5f) * 0.06f;
            }

            var wet = Wetness(region, shapes, p);
            return new Color(light + warmth, light, light - warmth, wet);
        }

        private static float Wetness(MapLandRegion region, MapShapes shapes, Vector2 p)
        {
            var near = 1f - MapGeometry.Smooth(shapes.RiverEdgeDistance(p) / WetReach);
            var floor = region != null && region.Kind == MapLandKind.Ravine ? 0.25f : 0f;
            return Mathf.Clamp01(Mathf.Max(near * 0.8f, floor));
        }

        private static bool Varies(MapLandKind kind) =>
            kind is MapLandKind.Field or MapLandKind.Garden or MapLandKind.Wood or MapLandKind.Back;

        private static Texture2D Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
