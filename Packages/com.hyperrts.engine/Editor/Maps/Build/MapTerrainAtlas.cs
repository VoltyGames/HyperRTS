using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// One texture array as a flipbook atlas PNG imported as a 2D array, one cell per terrain layer. The hash of its
    /// sources sits in the importer's user data, so an unchanged atlas is never rebuilt.
    /// </summary>
    internal static class MapTerrainAtlas
    {
        // Bump when cells are packed differently, so every atlas rebuilds.
        private const int PackingVersion = 1;
        private const int AnisoLevel = 8;

        internal static Texture2DArray Ensure(string path, IReadOnlyList<TerrainLayer> layers, int size,
            Func<TerrainLayer, Texture2D[]> sources, Func<TerrainLayer, Color32[]> cell, bool normalMap, bool sRGB)
        {
            var hash = Hash(layers, size, sources);
            if (!IsCurrent(path, hash))
            {
                var columns = Mathf.CeilToInt(Mathf.Sqrt(layers.Count));
                var rows = Mathf.CeilToInt(layers.Count / (float)columns);
                Write(path, layers, size, columns, rows, cell);
                Import(path, columns, rows, normalMap, sRGB, hash);
            }

            return AssetDatabase.LoadAssetAtPath<Texture2DArray>(path)
                ?? throw new InvalidOperationException($"HyperRTS: '{path}' did not import as a texture array.");
        }

        private static bool IsCurrent(string path, string hash)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            return importer != null && importer.userData == hash;
        }

        private static string Hash(IReadOnlyList<TerrainLayer> layers, int size, Func<TerrainLayer, Texture2D[]> sources)
        {
            var hash = new Hash128();
            hash.Append(PackingVersion);
            hash.Append(size);
            foreach (var layer in layers)
            {
                foreach (var texture in sources(layer))
                {
                    var source = AssetDatabase.GetAssetPath(texture);
                    hash.Append(source);
                    hash.Append(AssetDatabase.GetAssetDependencyHash(source).ToString());
                }
            }

            return hash.ToString();
        }

        /// <summary>Flipbook slices run left to right from the top row down; spare cells stay black.</summary>
        private static void Write(string path, IReadOnlyList<TerrainLayer> layers, int size, int columns, int rows,
            Func<TerrainLayer, Color32[]> cell)
        {
            var atlas = new Texture2D(columns * size, rows * size, TextureFormat.RGBA32, false, true);
            for (var i = 0; i < columns * rows; i++)
            {
                var pixels = i < layers.Count ? cell(layers[i]) : new Color32[size * size];
                atlas.SetPixels32(i % columns * size, (rows - 1 - i / columns) * size, size, size, pixels);
            }

            File.WriteAllBytes(FileUtil.GetPhysicalPath(path), atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
        }

        private static void Import(string path, int columns, int rows, bool normalMap, bool sRGB, string hash)
        {
            if (AssetImporter.GetAtPath(path) == null)
            {
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            settings.textureShape = TextureImporterShape.Texture2DArray;
            settings.flipbookColumns = columns;
            settings.flipbookRows = rows;
            settings.sRGBTexture = sRGB;
            settings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.alphaIsTransparency = false;
            settings.npotScale = TextureImporterNPOTScale.None;
            settings.mipmapEnabled = true;
            settings.wrapMode = TextureWrapMode.Repeat;
            settings.filterMode = FilterMode.Trilinear;
            settings.aniso = AnisoLevel;
            importer.SetTextureSettings(settings);
            importer.maxTextureSize = 16384;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.userData = hash;
            importer.SaveAndReimport();
        }
    }
}
