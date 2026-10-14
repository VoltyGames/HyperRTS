using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Simulation.Effects;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Paints one R8 <see cref="SurfaceMaterial"/> per land cell: the region's surface, the nearest road's class, or
    /// water.
    /// </summary>
    public sealed class MapGroundMaterialPainter
    {
        private readonly MapLand _land;
        private readonly MapShapes _shapes;
        private readonly MapClimate _climate;
        private readonly Dictionary<string, SurfaceMaterial> _surfaces = new();

        private MapGroundMaterialPainter(MapLand land, MapShapes shapes, MapClimate climate)
        {
            (_land, _shapes, _climate) = (land, shapes, climate);
        }

        // Point-filtered and uncompressed so every texel stays an exact enum value; readable for the baker.
        public static Texture2D Write(string path, MapLand land, MapShapes shapes, MapClimate climate)
        {
            var cells = Paint(land, shapes, climate);
            var pixels = new Color32[cells.Length];
            for (var i = 0; i < cells.Length; i++)
            {
                pixels[i] = new Color32(cells[i], 0, 0, 255);
            }

            return EditorTextures.WritePng(path, land.Width, land.Height, TextureFormat.R8, pixels, importer =>
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true;
                importer.maxTextureSize = 16384;
                var platform = importer.GetDefaultPlatformTextureSettings();
                platform.format = TextureImporterFormat.R8;
                importer.SetPlatformTextureSettings(platform);
            });
        }

        /// <summary>
        /// Row-major cells from the land's south-west corner, each a <see cref="SurfaceMaterial"/> byte.
        /// </summary>
        public static byte[] Paint(MapLand land, MapShapes shapes, MapClimate climate)
        {
            var painter = new MapGroundMaterialPainter(land, shapes, climate);
            var cells = new byte[land.Width * land.Height];
            land.ForEach((x, z, center) => cells[z * land.Width + x] = (byte)painter.Cell(x, z, center));
            return cells;
        }

        private SurfaceMaterial Cell(int x, int z, Vector2 center)
        {
            var region = _land.Region(x, z);
            if (region == null)
            {
                return Surface(_climate.groundSurface);
            }

            return region.Kind switch
            {
                MapLandKind.Water => SurfaceMaterial.Water,
                MapLandKind.Road => Road(center),
                _ => Surface(region.Surface),
            };
        }

        private SurfaceMaterial Road(Vector2 center)
        {
            if (!_shapes.NearestRoad(center, out var road, out _, out _, out _))
            {
                throw new System.InvalidOperationException($"HyperRTS: road land at {center} has no road shape.");
            }

            return MapImpactClass.Parse($"road class '{road.Class.key}'", road.Class.impactClass);
        }

        private SurfaceMaterial Surface(string key)
        {
            if (!_surfaces.TryGetValue(key, out var material))
            {
                material = MapImpactClass.Parse($"surface '{key}'", _climate.Surface(key).impactClass);
                _surfaces.Add(key, material);
            }

            return material;
        }
    }
}
