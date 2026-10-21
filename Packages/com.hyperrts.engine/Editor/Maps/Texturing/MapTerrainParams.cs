using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Texturing
{
    /// <summary>
    /// The per-layer parameters of HyperRTS/Terrain as a small float texture (materials can't store arrays), one
    /// column per layer; the row layout is documented in Terrain.shader. Kept as a sub-asset of the material.
    /// </summary>
    public static class MapTerrainParams
    {
        private const int Width = MapTerrainTextures.MaxLayers;
        private const int Rows = 5;

        internal static Texture2D Write(Material material, MapClimate climate)
        {
            var texture = SubAsset(material);
            var pixels = new Color[Width * Rows];
            var slope = climate.SurfaceLayer(climate.slopeSurface);
            for (var i = 0; i < climate.layers.Count; i++)
            {
                Column(pixels, i, climate.layers[i], i == slope);
            }

            texture.SetPixels(pixels);
            texture.Apply(false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static void Column(Color[] pixels, int layer, MapClimateLayer climateLayer, bool slope)
        {
            var terrainLayer = climateLayer.terrainLayer;
            var tint = terrainLayer.diffuseRemapMax;
            var low = terrainLayer.maskMapRemapMin;
            var high = terrainLayer.maskMapRemapMax;
            pixels[layer] = Mapping(climateLayer);
            pixels[Width + layer] = new Color(tint.x, tint.y, tint.z, climateLayer.macroStrength);
            pixels[2 * Width + layer] = new Color(high.w - low.w, low.w, high.y - low.y, low.y);
            pixels[3 * Width + layer] = Surface(climateLayer);
            pixels[4 * Width + layer] = Cover(climateLayer, slope);
        }

        /// <summary>Row 0: the layer's own UV turn and tiling; row layers add the row map's turn per pixel.</summary>
        public static Color Mapping(MapClimateLayer climateLayer)
        {
            var turn = climateLayer.rotation * Mathf.Deg2Rad;
            var tile = climateLayer.terrainLayer.tileSize;
            return new Color(Mathf.Cos(turn), Mathf.Sin(turn), 1f / tile.x, 1f / tile.y);
        }

        /// <summary>
        /// Row 3: height-blend weight, normal strength, hex rotation strength, and 0 for layers that follow rows.
        /// Those turn per pixel to the row map, turn no hex cell and shift it only along the rows, so furrows run on.
        /// </summary>
        public static Color Surface(MapClimateLayer climateLayer)
        {
            var rows = climateLayer.followsRows;
            var rotation = rows ? 0f : climateLayer.antiTiling;
            var acrossRows = rows ? 0f : 1f;
            return new Color(climateLayer.heightBlend, climateLayer.terrainLayer.normalScale, rotation, acrossRows);
        }

        /// <summary>
        /// Row 4: r how much the layer is grass cover, which sheds rain; g 1 for the slope surface's layer, which
        /// the shader projects sideways on steep ground.
        /// </summary>
        public static Color Cover(MapClimateLayer climateLayer, bool slope) =>
            new(climateLayer.grass, slope ? 1f : 0f, 0f, 0f);

        private static Texture2D SubAsset(Material material)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(material)))
            {
                if (asset is Texture2D existing)
                {
                    // A material built before the last row was added still holds the smaller texture.
                    existing.Reinitialize(Width, Rows);
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
