using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
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

        /// <summary>Renders a camera into <paramref name="target"/> and saves it, shader variants compiled first.</summary>
        public static void RenderPng(Camera camera, RenderTexture target, string path)
        {
            var asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                // Right after a build, variants not yet compiled would render as nothing (trees as bare bark).
                ShaderUtil.allowAsyncCompilation = false;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
            }

            SavePng(target, path);
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

        private static Texture2D WritePng(string path, int width, int height, TextureFormat format,
            Action<Texture2D> fill, Action<TextureImporter> configure)
        {
            var image = new Texture2D(width, height, format, false, true);
            try
            {
                fill(image);
                image.Apply(false);
                // The full path, since a local package's files are not under the project folder.
                File.WriteAllBytes(Path.GetFullPath(path), image.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(image);
            }

            return Import(path, configure);
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
