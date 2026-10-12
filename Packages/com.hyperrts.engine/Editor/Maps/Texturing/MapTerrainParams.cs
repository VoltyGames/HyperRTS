using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Texturing
{
    /// <summary>
    /// The per-layer parameters of HyperRTS/Terrain as a small float texture (materials can't store arrays), one
    /// column per layer; the row layout is documented in Terrain.shader. Kept as a sub-asset of the material.
    /// </summary>
    internal static class MapTerrainParams
    {
        private const int Width = MapTerrainTextures.MaxLayers;
        private const int Rows = 4;

        internal static Texture2D Write(Material material, MapClimate climate, float rowAngle)
        {
            var texture = SubAsset(material);
            var pixels = new Color[Width * Rows];
            for (var i = 0; i < climate.layers.Count; i++)
            {
                Column(pixels, i, climate.layers[i], rowAngle);
            }

            texture.SetPixels(pixels);
            texture.Apply(false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static void Column(Color[] pixels, int layer, MapClimateLayer climateLayer, float rowAngle)
        {
            var terrainLayer = climateLayer.terrainLayer;
            // Unturned, a texture's left-to-right axis points east (90°); this lays row textures along the rows.
            var rowTurn = climateLayer.followsRows ? rowAngle - 90f : 0f;
            var turn = (climateLayer.rotation + rowTurn) * Mathf.Deg2Rad;
            var tile = terrainLayer.tileSize;
            var tint = terrainLayer.diffuseRemapMax;
            var low = terrainLayer.maskMapRemapMin;
            var high = terrainLayer.maskMapRemapMax;
            var acrossRows = climateLayer.followsRows ? 0f : climateLayer.antiTiling;
            pixels[layer] = new Color(Mathf.Cos(turn), Mathf.Sin(turn), 1f / tile.x, 1f / tile.y);
            pixels[Width + layer] = new Color(tint.x, tint.y, tint.z, climateLayer.macroStrength);
            pixels[2 * Width + layer] = new Color(high.w - low.w, low.w, high.y - low.y, low.y);
            pixels[3 * Width + layer] = new Color(climateLayer.heightBlend, terrainLayer.normalScale,
                climateLayer.antiTiling, acrossRows);
        }

        private static Texture2D SubAsset(Material material)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(material)))
            {
                if (asset is Texture2D existing)
                {
                    return existing;
                }
            }

            var texture = new Texture2D(Width, Rows, TextureFormat.RGBAFloat, false, true)
            {
                name = material.name + " Layer Parameters",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            AssetDatabase.AddObjectToAsset(texture, material);
            return texture;
        }
    }
}
