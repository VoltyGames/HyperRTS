using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// The terrain's row map, one texel per land cell: the direction its crop or garden rows run (x east, y north) as
    /// a unit vector remapped to 0..1, moved through the cell's symmetry copy; (0.5, 0.5) where nothing grows in rows.
    /// </summary>
    public static class MapRowPainter
    {
        public static readonly Color NoRows = new(0.5f, 0.5f, 0f, 1f);

        // Bilinear, so a direction reaches half a cell past its region (splat edges bleed) and turns smoothly
        // between fields; the shader renormalises. No mips, which would average directions to nothing.
        public static Texture2D Write(string path, MapLand land, MapSymmetry symmetry) =>
            EditorTextures.WritePng(path, land.Width, land.Height, TextureFormat.RGBA32, Paint(land, symmetry),
                importer =>
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 16384;
                    var platform = importer.GetDefaultPlatformTextureSettings();
                    platform.format = TextureImporterFormat.RG16;
                    importer.SetPlatformTextureSettings(platform);
                });

        public static Color[] Paint(MapLand land, MapSymmetry symmetry)
        {
            var pixels = new Color[land.Width * land.Height];
            land.ForEach((x, z, center) => pixels[z * land.Width + x] = Pixel(land.Region(x, z), symmetry, center));
            return pixels;
        }

        private static Color Pixel(MapLandRegion region, MapSymmetry symmetry, Vector2 center)
        {
            if (region == null || !region.HasRows)
            {
                return NoRows;
            }

            symmetry.ToSector(center, out var copy);
            var along = symmetry.Copies[copy].Apply(MapGeometry.Forward(region.RowAngle));
            return new Color(along.x * 0.5f + 0.5f, along.y * 0.5f + 0.5f, 0f, 1f);
        }
    }
}
