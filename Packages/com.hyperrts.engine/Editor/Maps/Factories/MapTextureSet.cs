using System.IO;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>
    /// Loads a photo texture set (<c>T_&lt;Name&gt;_BaseColor</c> / <c>_Normal</c> / <c>_MaskMap</c> .png in one folder),
    /// setting each texture's import (type, colour space, size, high-quality compression) and reimporting only on change.
    /// </summary>
    public static class MapTextureSet
    {
        /// <summary>The base colour, normal and mask map of set <paramref name="name"/> in <paramref name="folder"/>.</summary>
        public static (Texture2D BaseColor, Texture2D Normal, Texture2D Mask) Read(string folder, string name, int maxSize) =>
            (BaseColor(folder, name, maxSize), Normal(folder, name, maxSize), Mask(folder, name, maxSize));

        public static Texture2D BaseColor(string folder, string name, int maxSize) =>
            Load($"{folder}/T_{name}_BaseColor.png", false, true, maxSize);

        public static Texture2D Normal(string folder, string name, int maxSize) =>
            Load($"{folder}/T_{name}_Normal.png", true, false, maxSize);

        /// <summary>Metallic R, occlusion G, height B, smoothness A.</summary>
        public static Texture2D Mask(string folder, string name, int maxSize) =>
            Load($"{folder}/T_{name}_MaskMap.png", false, false, maxSize);

        /// <summary>Base colour with real alpha (leaf and grass cards), clamped so cards don't bleed at their edges.</summary>
        public static Texture2D Cutout(string path, int maxSize)
        {
            var texture = Load(path, false, true, maxSize);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipMapsPreserveCoverage = true;
                importer.SaveAndReimport();
            }

            return texture;
        }

        private static Texture2D Load(string path, bool normal, bool srgb, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new FileNotFoundException($"HyperRTS: missing texture '{path}'.", path);
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (!Matches(importer, type, srgb, maxSize))
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = maxSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static bool Matches(TextureImporter importer, TextureImporterType type, bool srgb, int maxSize)
        {
            var sameType = importer.textureType == type;
            var sameSpace = importer.sRGBTexture == srgb;
            return sameType && sameSpace && importer.maxTextureSize == maxSize;
        }
    }
}
