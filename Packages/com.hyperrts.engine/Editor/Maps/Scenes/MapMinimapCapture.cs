using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Layout;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>Renders the open map straight down with an orthographic camera and saves it as the minimap picture.</summary>
    public static class MapMinimapCapture
    {
        private const int Size = 1024;
        private const float Altitude = 300f;

        public static Texture2D Capture(MapLayout layout, string path)
        {
            var camera = new GameObject("Minimap Capture").AddComponent<Camera>();
            try
            {
                camera.orthographic = true;
                camera.orthographicSize = layout.Size.y * 0.5f;
                camera.aspect = layout.Size.x / layout.Size.y;
                camera.nearClipPlane = 1f;
                camera.farClipPlane = Altitude + layout.Terrain.maxHeight;
                camera.transform.SetPositionAndRotation(new Vector3(layout.Bounds.center.x, Altitude, layout.Bounds.center.y),
                    Quaternion.Euler(90f, 0f, 0f));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                EditorTextures.RenderPng(camera, Size, Size, path);
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }

            return Import(path);
        }

        private static Texture2D Import(string path)
        {
            return EditorTextures.Import(path, importer =>
            {
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = Size;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
            });
        }
    }
}
