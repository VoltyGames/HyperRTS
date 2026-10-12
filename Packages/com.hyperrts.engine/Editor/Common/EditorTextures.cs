using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Common
{
    /// <summary>Texture asset import and GPU read-back shared by the map and environment tools.</summary>
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

        /// <summary>Writes a render texture to a PNG file as RGB.</summary>
        public static void SavePng(RenderTexture source, string path)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = source;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                File.WriteAllBytes(path, image.EncodeToPNG());
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
    }
}
