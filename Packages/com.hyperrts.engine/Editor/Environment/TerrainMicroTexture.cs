using HyperRTS.Editor.Common;
using HyperRTS.Editor.Generators;
using UnityEditor;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Editor.Environment
{
    /// <summary>
    /// Bakes the terrain's tileable micro detail: soil crumbs and grit as RG normal, B cavity occlusion and A albedo
    /// variation, which HyperRTS/Terrain lays over every layer near the camera.
    /// </summary>
    public sealed class TerrainMicroTexture : Generator
    {
        public const string Path = "Packages/com.hyperrts.engine/Presentation/Environment/TerrainMicro.png";
        public const int Size = 256;

        private const uint Seed = 20261021u;
        private const float Steepness = 0.4f;
        private const int CavityRadius = 8;
        private const float CavityDepth = 0.4f;
        private const float AlbedoContrast = 0.12f;

        public override string Name => "Terrain micro detail";

        public override string Group => "HyperRTS";

        public override int Order => 21;

        public override string Description => "Tileable soil grain normal, occlusion and albedo.";

        public override string Sources => "Seed and settings in TerrainMicroTexture.cs";

        public override string Outputs => Path;

        public override bool Available => EnginePackage.Writable;

        public override void Run()
        {
            var texels = Texels();
            var pixels = new Color32[texels.Length];
            for (var i = 0; i < texels.Length; i++)
            {
                pixels[i] = texels[i];
            }

            EditorTextures.WritePng(Path, Size, Size, TextureFormat.RGBA32, pixels, importer =>
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
            });
        }

        /// <summary>RG normal xy remapped to 0..1, B occlusion (1 open), A albedo variation centred on 0.5.</summary>
        public static Color[] Texels()
        {
            var random = new Random(Seed);
            var height = Height(ref random);
            var cavity = Cavity(height);
            var tone = TileableNoise.Lowpass(Size, 10f, ref random);
            var texels = new Color[height.Length];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var i = y * Size + x;
                    var normal = new Vector3(-TileableNoise.Sobel(height, Size, x, y, 1, 0) * Steepness,
                        -TileableNoise.Sobel(height, Size, x, y, 0, 1) * Steepness, 1f).normalized;
                    var occlusion = Mathf.Clamp01(1f + CavityDepth * Mathf.Min(cavity[i], 0f));
                    var albedo = Mathf.Clamp01(0.5f + AlbedoContrast * (0.6f * cavity[i] + 0.8f * tone[i]));
                    texels[i] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, occlusion, albedo);
                }
            }

            return texels;
        }

        /// <summary>Ridged clods about 4 to 8 cm across a 0.5 m tile, crumbs and finer grit on top.</summary>
        private static float[] Height(ref Random random)
        {
            var crumbs = TileableNoise.Lowpass(Size, 6f, ref random);
            var grains = TileableNoise.Lowpass(Size, 13f, ref random);
            var grit = TileableNoise.Lowpass(Size, 30f, ref random);
            for (var i = 0; i < crumbs.Length; i++)
            {
                crumbs[i] = 1f - 0.8f * Mathf.Abs(crumbs[i]) + 0.45f * grains[i] + 0.2f * grit[i];
            }

            return crumbs;
        }

        /// <summary>Height below its blurred surroundings: negative in the gaps between crumbs.</summary>
        private static float[] Cavity(float[] height)
        {
            var around = (float[])height.Clone();
            TileableNoise.Blur(around, Size, CavityRadius);
            var cavity = new float[height.Length];
            for (var i = 0; i < height.Length; i++)
            {
                cavity[i] = height[i] - around[i];
            }

            return cavity;
        }
    }
}
