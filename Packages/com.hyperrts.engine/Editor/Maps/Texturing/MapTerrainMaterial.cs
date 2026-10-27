using System;
using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Texturing
{
    /// <summary>
    /// Writes a map's HyperRTS/Terrain material for <c>Terrain.materialTemplate</c>: a variant of the climate's base
    /// material overriding the layer arrays, the per-layer parameters, the terrain's alphamaps and the ground tint and row maps.
    /// </summary>
    public static class MapTerrainMaterial
    {
        public const string ShaderName = "HyperRTS/Terrain";

        private static readonly int AlbedoArray = Shader.PropertyToID("_AlbedoArray");
        private static readonly int NormalArray = Shader.PropertyToID("_NormalArray");
        private static readonly int MaskArray = Shader.PropertyToID("_MaskArray");
        private static readonly int LayerParams = Shader.PropertyToID("_LayerParams");
        private static readonly int LayerCount = Shader.PropertyToID("_LayerCount");
        private static readonly int HeightBlendDepth = Shader.PropertyToID("_HeightBlendDepth");
        private static readonly int GroundTintMap = Shader.PropertyToID("_GroundTintMap");
        private static readonly int RowMap = Shader.PropertyToID("_RowMap");

        private static readonly int[] Controls =
        {
            Shader.PropertyToID("_Control0"), Shader.PropertyToID("_Control1"), Shader.PropertyToID("_Control2"),
            Shader.PropertyToID("_Control3"),
        };

        /// <summary>
        /// <paramref name="groundTint"/> is linear over the whole terrain: RGB tint (0.5 neutral), A wetness.
        /// <paramref name="rows"/> is linear too: RG the row direction remapped to 0..1, (0.5, 0.5) for none.
        /// </summary>
        public static Material Write(string path, MapClimate climate, TerrainData data, Texture2D groundTint, Texture2D rows)
        {
            Require(climate, data, groundTint);
            RequireLinear(rows, "row map");
            var (albedo, normal, mask) = MapTerrainTextures.Ensure(climate);
            var material = LoadOrCreate(path, climate);
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

            material.SetTexture(GroundTintMap, groundTint);
            material.SetTexture(RowMap, rows);
            EditorUtility.SetDirty(material);
            // Saved now: opening the map's scene unloads an unsaved material and drops every override.
            AssetDatabase.SaveAssetIfDirty(material);
            if (!material.IsPropertyOverriden(LayerCount))
            {
                throw new InvalidOperationException(
                    $"HyperRTS: terrain material '{path}' lost its overrides while saving, so the ground would render as one flat layer.");
            }

            return material;
        }

        private static void Require(MapClimate climate, TerrainData data, Texture2D groundTint)
        {
            if (climate.terrain.shader.name != ShaderName)
            {
                throw new ArgumentException(
                    $"HyperRTS: climate '{climate.name}' terrain material '{climate.terrain.name}' must use {ShaderName}.");
            }

            if (data.alphamapLayers != climate.layers.Count)
            {
                throw new ArgumentException(
                    $"HyperRTS: '{data.name}' paints {data.alphamapLayers} layers but climate '{climate.name}' has {climate.layers.Count}.");
            }

            RequireLinear(groundTint, "ground tint map");
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

        // No RevertAllPropertyOverrides: Write sets every override it owns, and on Unity 7 alpha a revert can land
        // after those sets and save the variant empty.
        private static Material LoadOrCreate(string path, MapClimate climate)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(climate.terrain);
                AssetDatabase.CreateAsset(material, path);
            }

            material.parent = climate.terrain;
            return material;
        }
    }
}
