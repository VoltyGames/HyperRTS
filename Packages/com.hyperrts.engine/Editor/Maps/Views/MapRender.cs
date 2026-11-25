using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>Renders a map camera to a PNG in one frame, with shader variants compiled and Surface Cache GI off.</summary>
    internal static class MapRender
    {
        /// <summary>Renders a camera to a width × height PNG.</summary>
        public static void WritePng(Camera camera, int width, int height, string path)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            try
            {
                Render(camera, target, path);
                EditorTextures.WritePng(target, path);
            }
            finally
            {
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
    }
}
