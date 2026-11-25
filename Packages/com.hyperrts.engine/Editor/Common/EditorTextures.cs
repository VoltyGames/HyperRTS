using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Common
{
    /// <summary>Texture asset import, PNG writing and GPU read-back shared by the editor tools.</summary>
    internal static class EditorTextures
    {
        /// <summary>
        /// Imports the texture file at <paramref name="path"/> (creating its importer on first sight), applies
        /// <paramref name="configure"/> to the importer, reimports once and returns the loaded texture.
        /// </summary>
        public static Texture2D Import(string path, Action<TextureImporter> configure)
        {
            if (AssetImporter.GetAtPath(path) == null)
            {
                AssetDatabase.ImportAsset(path);
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"HyperRTS: '{path}' has no texture importer.");
            }

            configure(importer);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Writes the pixels as a PNG in <paramref name="format"/>, then <see cref="Import"/>s it.</summary>
        public static Texture2D WritePng(string path, int width, int height, TextureFormat format, Color32[] pixels,
            Action<TextureImporter> configure) =>
            WritePng(path, width, height, format, image => image.SetPixels32(pixels), configure);

        /// <inheritdoc cref="WritePng(string, int, int, TextureFormat, Color32[], Action{TextureImporter})"/>
        public static Texture2D WritePng(string path, int width, int height, TextureFormat format, Color[] pixels,
            Action<TextureImporter> configure) =>
            WritePng(path, width, height, format, image => image.SetPixels(pixels), configure);

        /// <inheritdoc cref="WritePng(string, int, int, TextureFormat, Color32[], Action{TextureImporter})"/>
        public static Texture2D WritePng(string path, int width, int height, TextureFormat format, byte[] pixels,
            Action<TextureImporter> configure) =>
            WritePng(path, width, height, format, image => image.SetPixelData(pixels, 0), configure);

        /// <summary>Reads a render texture back and saves it as a PNG, without importing it.</summary>
        public static void WritePng(RenderTexture source, string path)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = source;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                SavePng(image, path);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
            }
        }

        /// <summary>
        /// Any texture, readable or not, blitted to a linear size × size texture on the GPU and read back; the caller
        /// destroys the result.
        /// </summary>
        public static Texture2D Resample(Texture source, int size, RenderTextureFormat renderFormat, TextureFormat format)
        {
            var target = RenderTexture.GetTemporary(size, size, 0, renderFormat, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                var result = new Texture2D(size, size, format, false, true);
                result.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static Texture2D WritePng(string path, int width, int height, TextureFormat format,
            Action<Texture2D> fill, Action<TextureImporter> configure)
        {
            var image = new Texture2D(width, height, format, false, true);
            try
            {
                fill(image);
                SavePng(image, path);
            }
            finally
            {
                Object.DestroyImmediate(image);
            }

            return Import(path, configure);
        }

        /// <summary>Writes a PNG file without importing it; the physical path, since a local package's files are not under the project folder.</summary>
        public static void SavePng(Texture2D image, string path) =>
            File.WriteAllBytes(FileUtil.GetPhysicalPath(path), image.EncodeToPNG());
    }
}
