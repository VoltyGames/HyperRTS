using System.IO;
using HyperRTS.Editor.Maps.Layout;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>Renders the open map straight down with an orthographic camera and saves it as the minimap picture.</summary>
    public static class MapMinimapCapture
    {
        private const int Size = 1024;
        private const float Altitude = 300f;

        public static Texture2D Capture(MapLayout layout, string path)
        {
            var camera = new GameObject("Minimap Capture").AddComponent<Camera>();
            var target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            var fog = RenderSettings.fog;
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
                RenderSettings.fog = false;
                var request = new RenderPipeline.StandardRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                Save(target, path);
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(camera.gameObject);
            }

            return Import(path);
        }

        private static void Save(RenderTexture target, string path)
        {
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        private static Texture2D Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
