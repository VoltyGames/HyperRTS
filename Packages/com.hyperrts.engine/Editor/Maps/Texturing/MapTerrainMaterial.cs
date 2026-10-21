using System;
using HyperRTS.Editor.Environment;
using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Texturing
{
    /// <summary>
    /// Writes a map's HyperRTS/Terrain material for <c>Terrain.materialTemplate</c>: the climate's layer arrays, the
    /// per-layer parameters, the terrain's alphamaps and the map's macro and row maps.
    /// </summary>
    public static class MapTerrainMaterial
    {
        public const string ShaderName = "HyperRTS/Terrain";
        private const string NoisePath = "Packages/com.hyperrts.engine/Presentation/Environment/TerrainNoise.png";

        private static readonly int AlbedoArray = Shader.PropertyToID("_AlbedoArray");
        private static readonly int NormalArray = Shader.PropertyToID("_NormalArray");
        private static readonly int MaskArray = Shader.PropertyToID("_MaskArray");
        private static readonly int LayerParams = Shader.PropertyToID("_LayerParams");
        private static readonly int LayerCount = Shader.PropertyToID("_LayerCount");
        private static readonly int HeightBlendDepth = Shader.PropertyToID("_HeightBlendDepth");
        private static readonly int MacroMap = Shader.PropertyToID("_MacroMap");
        private static readonly int RowMap = Shader.PropertyToID("_RowMap");
        private static readonly int NoiseMap = Shader.PropertyToID("_NoiseMap");
        private static readonly int MicroMap = Shader.PropertyToID("_MicroMap");

        /// <summary>Close-up detail every build writes, so a rebuilt material never keeps hand-tuned values.</summary>
        private static readonly (string Property, float Value)[] Detail =
        {
            ("_MicroTiling", 0.5f), ("_MicroNormal", 1.4f), ("_MicroOcclusion", 0.9f), ("_MicroAlbedo", 1.3f),
            ("_MicroStart", 24f), ("_MicroEnd", 42f), ("_TriplanarStart", 30f), ("_TriplanarFull", 42f),
            ("_ParallaxDepth", 0.1f), ("_ParallaxEnd", 38f),
        };

        private static readonly int[] Controls =
        {
            Shader.PropertyToID("_Control0"), Shader.PropertyToID("_Control1"), Shader.PropertyToID("_Control2"),
            Shader.PropertyToID("_Control3"),
        };

        /// <summary>
        /// <paramref name="macro"/> is linear over the whole terrain: RGB tint (0.5 neutral), A wetness.
        /// <paramref name="rows"/> is linear too: RG the row direction remapped to 0..1, (0.5, 0.5) for none.
        /// </summary>
        public static Material Write(string path, MapClimate climate, TerrainData data, Texture2D macro, Texture2D rows)
        {
            Require(climate, data, macro);
            RequireLinear(rows, "row map");
            var (albedo, normal, mask) = MapTerrainTextures.Ensure(climate);
            var material = LoadOrCreate(path);
            material.SetTexture(AlbedoArray, albedo);
            material.SetTexture(NormalArray, normal);
            material.SetTexture(MaskArray, mask);
            material.SetTexture(LayerParams, MapTerrainParams.Write(material, climate));
            material.SetFloat(LayerCount, climate.layers.Count);
            material.SetFloat(HeightBlendDepth, climate.heightBlendDepth);
            var alphamaps = data.alphamapTextures;
            for (var i = 0; i < Controls.Length; i++)
            {
                material.SetTexture(Controls[i], i < alphamaps.Length ? alphamaps[i] : null);
            }

            material.SetTexture(MacroMap, macro);
            material.SetTexture(RowMap, rows);
            material.SetTexture(NoiseMap, Engine(NoisePath, "terrain noise"));
            material.SetTexture(MicroMap, Engine(TerrainMicroTexture.Path, "terrain micro detail"));
            foreach (var (property, value) in Detail)
            {
                material.SetFloat(property, value);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Engine(string path, string role) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(path)
            ?? throw new InvalidOperationException($"HyperRTS: the {role} '{path}' is missing.");

        private static void Require(MapClimate climate, TerrainData data, Texture2D macro)
        {
            if (data.alphamapLayers != climate.layers.Count)
            {
                throw new ArgumentException(
                    $"HyperRTS: '{data.name}' paints {data.alphamapLayers} layers but climate '{climate.name}' has {climate.layers.Count}.");
            }

            RequireLinear(macro, "macro map");
        }

        private static void RequireLinear(Texture2D texture, string role)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture), $"HyperRTS: the terrain material needs a {role}.");
            }

            if (texture.isDataSRGB)
            {
                throw new ArgumentException($"HyperRTS: {role} '{texture.name}' must be imported linear (sRGB off), so 0.5 is neutral.");
            }
        }

        private static Material LoadOrCreate(string path)
        {
            var shader = Shader.Find(ShaderName)
                ?? throw new InvalidOperationException($"HyperRTS: the {ShaderName} shader is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            return material;
        }
    }
}
