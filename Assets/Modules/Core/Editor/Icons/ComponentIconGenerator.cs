using System.IO;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Core.Editor.Icons
{
    /// <summary>Generates the per-module component icons referenced by <see cref="HyperRTSIcons"/>. Regenerate on demand.</summary>
    public static class ComponentIconGenerator
    {
        private const int Size = 64;

        // Each module's icon path (single source of truth: HyperRTSIcons) paired with its accent colour.
        private static readonly (string IconPath, Color Color)[] Modules =
        {
            (HyperRTSIcons.Attack, new Color(0.85f, 0.25f, 0.22f)),
            (HyperRTSIcons.Buildings, new Color(0.45f, 0.50f, 0.58f)),
            (HyperRTSIcons.Cameras, new Color(0.55f, 0.40f, 0.85f)),
            (HyperRTSIcons.Health, new Color(0.30f, 0.75f, 0.35f)),
            (HyperRTSIcons.Resources, new Color(0.95f, 0.70f, 0.20f)),
            (HyperRTSIcons.Selection, new Color(0.30f, 0.70f, 1.00f)),
            (HyperRTSIcons.Units, new Color(0.25f, 0.50f, 0.90f)),
        };

        [MenuItem("RTS/Tools/Generate Component Icons")]
        public static void Generate()
        {
            foreach (var (iconPath, color) in Modules)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(iconPath));
                File.WriteAllBytes(iconPath, BuildIcon(color).EncodeToPNG());
                AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
                ApplyImportSettings(iconPath);
            }

            AssetDatabase.Refresh();
            Debug.Log($"Generated {Modules.Length} component icons.");
        }

        // Rounded-square filled with the accent colour on a transparent background.
        private static Texture2D BuildIcon(Color accent)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            const float margin = 6f;
            const float radius = 14f;
            const float half = Size / 2f - margin;
            const float center = (Size - 1) / 2f;

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var d = RoundedBoxSdf(x - center, y - center, half, half, radius);
                    var alpha = Mathf.Clamp01(0.5f - d); // ~1px anti-aliased edge
                    tex.SetPixel(x, y, new Color(accent.r, accent.g, accent.b, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        private static float RoundedBoxSdf(float px, float py, float halfW, float halfH, float r)
        {
            var qx = Mathf.Abs(px) - halfW + r;
            var qy = Mathf.Abs(py) - halfH + r;
            var outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            var inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - r;
        }

        private static void ApplyImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.GUI;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
