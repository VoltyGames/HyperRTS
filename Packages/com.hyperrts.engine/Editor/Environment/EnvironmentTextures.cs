using System;
using System.IO;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Environment
{
    /// <summary>
    /// HyperRTS ▸ Generate Environment Textures: bakes the tileable cloud cookie the sun casts as cloud shadows and the
    /// tileable noise that breaks up road edges into Presentation/Environment.
    /// </summary>
    public static class EnvironmentTextures
    {
        private const string Folder = "Packages/com.hyperrts.engine/Presentation/Environment/";
        public const string CloudCookiePath = Folder + "CloudCookie.png";
        public const string EdgeNoisePath = Folder + "EdgeNoise.png";

        private const int CloudSize = 512;
        private const int CloudPeriod = 2;
        private const float CloudCover = 0.35f;
        private const float ThickestCloud = 0.65f;
        private const int Penumbra = 6;
        private const int NoiseSize = 256;
        private const int NoisePeriod = 8;

        [MenuItem(EditorMenu.Root + "Generate Environment Textures", false, EditorMenu.GenerateIconsPriority)]
        public static void Generate()
        {
            Write(CloudCookiePath, Clouds(), CloudSize);
            Write(EdgeNoisePath, EdgeNoise(), NoiseSize);
        }

        /// <summary>
        /// Soft, lumpy cumulus over about a third of the tile: open sky is 1 and the thickest cloud lets 65% of the sun
        /// through, so shadows stay subtle.
        /// </summary>
        public static float[] Clouds()
        {
            var density = Field(CloudSize, CloudPeriod, p => TileableNoise.Fbm(p, CloudPeriod, 4, 3));
            var edge = TileableNoise.Percentile(density, 1f - CloudCover);
            var core = TileableNoise.Percentile(density, 0.995f);
            for (var i = 0; i < density.Length; i++)
            {
                var cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge, core, density[i]));
                density[i] = 1f - cloud * (1f - ThickestCloud);
            }

            // The sun's width and the cloud's depth blur a real cloud shadow's edge over metres.
            TileableNoise.Blur(density, CloudSize, Penumbra);
            return density;
        }

        /// <summary>Fine fBm stretched over 0..1.</summary>
        public static float[] EdgeNoise()
        {
            var values = Field(NoiseSize, NoisePeriod, p => TileableNoise.Fbm(p, NoisePeriod, 5, 7));
            var low = TileableNoise.Percentile(values, 0.02f);
            var high = TileableNoise.Percentile(values, 0.98f);
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = Mathf.InverseLerp(low, high, values[i]);
            }

            return values;
        }

        /// <summary>A square of samples spanning <paramref name="period"/> lattice cells, so it tiles.</summary>
        private static float[] Field(int size, int period, Func<Vector2, float> sample)
        {
            var values = new float[size * size];
            var scale = (float)period / size;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    values[y * size + x] = sample(new Vector2(x + 0.5f, y + 0.5f) * scale);
                }
            }

            return values;
        }

        private static void Write(string path, float[] values, int size)
        {
            var pixels = new byte[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                pixels[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(values[i]) * 255f);
            }

            var texture = new Texture2D(size, size, TextureFormat.R8, false, true);
            texture.SetPixelData(pixels, 0);
            texture.Apply(false);
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            EditorTextures.Import(path, Configure);
        }

        /// <summary>One linear channel in red (the light cookie reads it as a red-format cookie), tiling, with mips.</summary>
        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.SingleChannel;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
        }
    }
}
