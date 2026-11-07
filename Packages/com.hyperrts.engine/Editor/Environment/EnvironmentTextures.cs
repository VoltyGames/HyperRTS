using System.Collections.Generic;
using System;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Generators;
using UnityEditor;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Editor.Environment
{
    /// <summary>
    /// Bakes tileable textures into Presentation/Environment: the cloud shadow cookie, road edge noise, terrain
    /// variation noise and the river ripple normal map.
    /// </summary>
    public sealed class EnvironmentTextures : Generator
    {
        private const string Folder = "Packages/com.hyperrts.engine/Presentation/Environment/";
        public const string CloudCookiePath = Folder + "Weather/CloudCookie.png";
        public const string EdgeNoisePath = Folder + "EdgeNoise.png";
        public const string TerrainNoisePath = Folder + "Ground/TerrainNoise.png";
        public const string WaterRipplesPath = Folder + "Water/WaterRipples.png";

        private const int CloudSize = 512;
        private const int CloudPeriod = 2;
        private const float CloudCover = 0.35f;
        private const float ThickestCloud = 0.65f;
        private const int Penumbra = 6;
        private const int NoiseSize = 256;
        private const int NoisePeriod = 8;
        public const int TerrainSize = 256;
        private const uint TerrainSeed = 20261011u;
        private const float TerrainContrast = 0.17f;
        public const int RippleSize = 1024;
        private const uint RippleSeed = 7u;
        private const float RipplePeakCycles = 7f;
        private const float RippleSlope = 0.4f;

        public override string Name => "Environment textures";

        public override string Group => "HyperRTS";

        public override int Order => 20;

        public override string Description =>
            "Cloud shadow cookie, road edge noise, terrain noise and water ripple normals.";

        public override string Sources => "Seeds and settings in EnvironmentTextures.cs";

        public override IEnumerable<string> Inputs => new[] { ScriptPath() };

        public override string Outputs => $"{CloudCookiePath}, {EdgeNoisePath}, {TerrainNoisePath}, {WaterRipplesPath}";

        public override bool Available => EnginePackage.Writable;

        public override void Run()
        {
            WriteGray(CloudCookiePath, Clouds(), CloudSize);
            WriteGray(EdgeNoisePath, EdgeNoise(), NoiseSize);
            WriteRGB(TerrainNoisePath, TerrainSize, TerrainNoise(), v => (byte)Mathf.Floor(Mathf.Clamp01(v) * 255f + 0.5f),
                ConfigureTerrainNoise);
            WriteRGB(WaterRipplesPath, RippleSize, RippleNormals(), v => (byte)Mathf.Floor((v * 0.5f + 0.5f) * 255f),
                ConfigureWaterRipples);
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

        /// <summary>R is a flat-histogram selector, G broad and B fine brightness variation, both centred on 0.5.</summary>
        public static Vector3[] TerrainNoise()
        {
            var random = new Random(TerrainSeed);
            var selector = TileableNoise.Lowpass(TerrainSize, 10f, ref random);
            TileableNoise.RankUniform(selector);
            var broad = Brightness(TileableNoise.Lowpass(TerrainSize, 4f, ref random), 0.8f,
                TileableNoise.Lowpass(TerrainSize, 9f, ref random), 0.6f);
            var fine = Brightness(TileableNoise.Lowpass(TerrainSize, 18f, ref random), 0.7f,
                TileableNoise.Lowpass(TerrainSize, 40f, ref random), 0.5f);
            var texels = new Vector3[selector.Length];
            for (var i = 0; i < texels.Length; i++)
            {
                texels[i] = new Vector3(selector[i], broad[i], fine[i]);
            }

            return texels;
        }

        /// <summary>Unit tangent-space normals of a tiling wind-wave spectrum, v up as Unity samples them.</summary>
        public static Vector3[] RippleNormals() =>
            WaterSpectrum.Normals(RippleSize, RippleSeed, RipplePeakCycles, RippleSlope);

        /// <summary>Two standardised fields weighted, scaled about 0.5 and clipped to 0..1, written into the first.</summary>
        private static float[] Brightness(float[] first, float firstWeight, float[] second, float secondWeight)
        {
            for (var i = 0; i < first.Length; i++)
            {
                first[i] = Mathf.Clamp01(0.5f + TerrainContrast * (first[i] * firstWeight + second[i] * secondWeight));
            }

            return first;
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

        private static void WriteGray(string path, float[] values, int size)
        {
            var pixels = new byte[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                pixels[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(values[i]) * 255f);
            }

            WriteTiling(path, size, TextureFormat.R8, pixels, Configure);
        }

        private static void WriteRGB(string path, int size, Vector3[] texels, Func<float, byte> encode,
            Action<TextureImporter> configure)
        {
            var pixels = new byte[texels.Length * 3];
            for (var i = 0; i < texels.Length; i++)
            {
                pixels[i * 3] = encode(texels[i].x);
                pixels[i * 3 + 1] = encode(texels[i].y);
                pixels[i * 3 + 2] = encode(texels[i].z);
            }

            WriteTiling(path, size, TextureFormat.RGB24, pixels, configure);
        }

        /// <summary>Every baked texture tiles and has mips; <paramref name="configure"/> sets the rest.</summary>
        private static void WriteTiling(string path, int size, TextureFormat format, byte[] pixels,
            Action<TextureImporter> configure) =>
            EditorTextures.WritePng(path, size, size, format, pixels, importer =>
            {
                configure(importer);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
            });

        /// <summary>One linear channel in red, since the light cookie reads it as a red-format cookie.</summary>
        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.SingleChannel;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture = false;
        }

        /// <summary>Linear and uncompressed, since the terrain shader reads each channel as separate data.</summary>
        private static void ConfigureTerrainNoise(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        // Seen at a grazing angle from the match camera, so trilinear and anisotropic, and full size.
        private static void ConfigureWaterRipples(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = RippleSize;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
        }
    }
}
