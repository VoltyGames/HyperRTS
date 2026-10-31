using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Texturing
{
    /// <summary>
    /// The texture arrays HyperRTS/Terrain samples for a climate's layers, kept in a Generated folder next to the
    /// climate's library asset and named after it: albedo with the mask's height in alpha, normal maps, and occlusion (R) with smoothness (G). Each is
    /// rebuilt only when its source textures or the layers change.
    /// </summary>
    public static class MapTerrainTextures
    {
        public const int MaxLayers = 16;

        /// <summary>Width and height of every array slice; sources of other sizes are resampled.</summary>
        public const int SliceSize = 1024;

        public static (Texture2DArray Albedo, Texture2DArray Normal, Texture2DArray Mask) Ensure(MapClimate climate)
        {
            var problem = Problems(climate).FirstOrDefault();
            if (problem != null)
            {
                throw new ArgumentException($"HyperRTS: climate '{climate.key}': {problem}");
            }

            var layers = climate.layers.Select(l => l.terrainLayer).ToList();
            var folder = GeneratedFolder(climate);
            var albedo = MapTerrainAtlas.Ensure($"{folder}/{climate.key}_TerrainAlbedo.png", layers, SliceSize,
                l => new[] { l.diffuseTexture, l.maskMapTexture }, AlbedoCell, normalMap: false, sRGB: true);
            var normal = MapTerrainAtlas.Ensure($"{folder}/{climate.key}_TerrainNormal.png", layers, SliceSize,
                l => new[] { l.normalMapTexture }, l => MapTerrainSource.Read(l.normalMapTexture, SliceSize),
                normalMap: true, sRGB: false);
            var mask = MapTerrainAtlas.Ensure($"{folder}/{climate.key}_TerrainMask.png", layers, SliceSize,
                l => new[] { l.maskMapTexture }, MaskCell, normalMap: false, sRGB: false);
            return (albedo, normal, mask);
        }

        /// <summary>What keeps the climate's layers from packing into the arrays, one message each.</summary>
        public static IEnumerable<string> Problems(MapClimate climate)
        {
            if (climate.layers.Count == 0 || climate.layers.Count > MaxLayers)
            {
                yield return $"{climate.layers.Count} layers; use 1 to {MaxLayers}.";
            }

            foreach (var layer in climate.layers)
            {
                if (layer.terrainLayer == null)
                {
                    yield return $"layer '{layer.key}' has no Terrain Layer.";
                    continue;
                }

                foreach (var problem in TextureProblems(layer))
                {
                    yield return $"layer '{layer.key}' {problem}";
                }
            }
        }

        private static IEnumerable<string> TextureProblems(MapClimateLayer layer)
        {
            var terrainLayer = layer.terrainLayer;
            var textures = new[]
            {
                ("diffuse texture", terrainLayer.diffuseTexture), ("normal map", terrainLayer.normalMapTexture),
                ("mask map", terrainLayer.maskMapTexture),
            };
            foreach (var (role, texture) in textures)
            {
                if (texture == null)
                {
                    yield return $"has no {role}.";
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(texture);
                if (!MapTerrainSource.IsReadable(path))
                {
                    yield return $"has a {role} '{path}' that is not a PNG or JPG file.";
                }
            }

            if (terrainLayer.maskMapTexture == null)
            {
                yield break;
            }

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(terrainLayer.maskMapTexture));
            if (importer is TextureImporter mask && !mask.DoesSourceTextureHaveAlpha())
            {
                yield return $"has a mask map '{mask.assetPath}' without alpha: smoothness belongs in its alpha.";
            }
        }

        private static string GeneratedFolder(MapClimate climate)
        {
            if (string.IsNullOrEmpty(climate.file))
            {
                throw new ArgumentException($"HyperRTS: climate '{climate.key}' must come from a map library asset.");
            }

            var parent = Path.GetDirectoryName(climate.file)?.Replace('\\', '/');
            var folder = $"{parent}/Generated";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(parent, "Generated");
            }

            return folder;
        }

        private static Color32[] AlbedoCell(TerrainLayer layer)
        {
            var albedo = MapTerrainSource.Read(layer.diffuseTexture, SliceSize);
            var heights = MapTerrainSource.Heights(MapTerrainSource.Read(layer.maskMapTexture, SliceSize));
            for (var i = 0; i < albedo.Length; i++)
            {
                albedo[i].a = heights[i];
            }

            return albedo;
        }

        private static Color32[] MaskCell(TerrainLayer layer)
        {
            var mask = MapTerrainSource.Read(layer.maskMapTexture, SliceSize);
            for (var i = 0; i < mask.Length; i++)
            {
                mask[i] = new Color32(mask[i].g, mask[i].a, 0, 255);
            }

            return mask;
        }
    }
}
