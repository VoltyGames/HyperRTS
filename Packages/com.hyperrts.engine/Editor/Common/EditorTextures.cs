using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Common
{
    /// <summary>Texture asset import, PNG writing and GPU read-back shared by the map and environment tools.</summary>
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

        /// <summary>Renders a camera to a width × height PNG.</summary>
        public static void RenderPng(Camera camera, int width, int height, string path)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                Render(camera, target, path);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                SavePng(image, path);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
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

        /// <summary>
        /// Renders with shader variants compiled first and Surface Cache GI off. Throws if the frame fails, which would
        /// leave the target blank.
        /// </summary>
        private static void Render(Camera camera, RenderTexture target, string path)
        {
            string failure = null;
            void Record(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Exception)
                {
                    failure ??= message;
                }
            }

            var surfaceCaches = ActiveSurfaceCaches();
            var asyncCompilation = ShaderUtil.allowAsyncCompilation;
            Application.logMessageReceived += Record;
            try
            {
                // Right after a build, variants not yet compiled would render as nothing (trees as bare bark).
                ShaderUtil.allowAsyncCompilation = false;
                surfaceCaches.ForEach(feature => feature.SetActive(false));
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            }
            finally
            {
                surfaceCaches.ForEach(feature => feature.SetActive(true));
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                Application.logMessageReceived -= Record;
            }

            if (failure != null)
            {
                throw new InvalidOperationException($"HyperRTS: rendering '{path}' failed: {failure}");
            }
        }

        /// <summary>
        /// Surface Cache GI builds up over frames, so a one-frame capture lights by the ambient probe instead. On Unity 7
        /// alpha its update also throws after a map build replaces the scene, which blanks the frame.
        /// </summary>
        private static List<SurfaceCacheGIRendererFeature> ActiveSurfaceCaches() =>
            UniversalRenderPipeline.asset.rendererDataList.ToArray()
                .SelectMany(data => data.rendererFeatures)
                .OfType<SurfaceCacheGIRendererFeature>()
                .Where(feature => feature.isActive)
                .ToList();

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

        // The full path, since a local package's files are not under the project folder.
        private static void SavePng(Texture2D image, string path) =>
            File.WriteAllBytes(Path.GetFullPath(path), image.EncodeToPNG());
    }
}
