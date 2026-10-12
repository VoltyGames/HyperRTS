using System;
using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Texturing
{
    /// <summary>
    /// Writes a map's HyperRTS/Terrain material for <c>Terrain.materialTemplate</c>: the climate's layer arrays, the
    /// per-layer parameters, the terrain's alphamaps and the map's macro map.
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
        private static readonly int MacroMap = Shader.PropertyToID("_MacroMap");
        private static readonly int NoiseMap = Shader.PropertyToID("_NoiseMap");

        private static readonly int[] Controls =
        {
            Shader.PropertyToID("_Control0"), Shader.PropertyToID("_Control1"), Shader.PropertyToID("_Control2"),
            Shader.PropertyToID("_Control3"),
        };

        /// <summary>
        /// <paramref name="macro"/> is linear over the whole terrain: RGB tint (0.5 neutral), A wetness.
        /// <paramref name="rowAngle"/> is the crop rows' direction (degrees clockwise from north) that layers following
        /// rows turn to.
        /// </summary>
        public static Material Write(string path, MapClimate climate, TerrainData data, Texture2D macro, float rowAngle)
        {
            Require(climate, data, macro);
            var (albedo, normal, mask) = MapTerrainTextures.Ensure(climate);
            var material = LoadOrCreate(path);
            material.SetTexture(AlbedoArray, albedo);
            material.SetTexture(NormalArray, normal);
            material.SetTexture(MaskArray, mask);
            material.SetTexture(LayerParams, MapTerrainParams.Write(material, climate, rowAngle));
            material.SetFloat(LayerCount, climate.layers.Count);
            var alphamaps = data.alphamapTextures;
            for (var i = 0; i < Controls.Length; i++)
            {
                material.SetTexture(Controls[i], i < alphamaps.Length ? alphamaps[i] : null);
            }

            material.SetTexture(MacroMap, macro);
            material.SetTexture(NoiseMap, AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath)
                ?? throw new InvalidOperationException($"HyperRTS: the terrain noise '{NoisePath}' is missing."));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Require(MapClimate climate, TerrainData data, Texture2D macro)
        {
            if (data.alphamapLayers != climate.layers.Count)
            {
                throw new ArgumentException(
                    $"HyperRTS: '{data.name}' paints {data.alphamapLayers} layers but climate '{climate.name}' has {climate.layers.Count}.");
            }

            if (macro == null)
            {
                throw new ArgumentNullException(nameof(macro), "HyperRTS: the terrain material needs a macro map.");
            }

            if (macro.isDataSRGB)
            {
                throw new ArgumentException($"HyperRTS: macro map '{macro.name}' must be imported linear (sRGB off), so 0.5 is neutral.");
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
