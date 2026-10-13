using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A terrain texture layer, how the terrain shader lays it on the ground, and the grass that grows wherever it is painted.</summary>
    [Serializable]
    [MapLibraryItem("layer", "terrainLayer", PreviewRefs = new[] { "terrainLayer" })]
    public sealed class MapClimateLayer : IMapKeyed, IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [Tooltip("Diffuse, normal and mask maps (mask: occlusion G, height B, smoothness A), tile size, normal scale, tint (Diffuse Remap Max) and mask remap.")]
        [NonSerialized]
        public TerrainLayer terrainLayer;

        [Tooltip("Colour of this ground in the report's overview image.")]
        public Color mapColor = Color.grey;

        [Tooltip("Turns the texture clockwise on the ground (degrees); at 0 the image's top faces north.")]
        public float rotation;

        [Tooltip("Lays the texture's rows, which run left to right in the image, along the map's crop rows (Rotation turns them further).")]
        public bool followsRows;

        [Tooltip("How far the texture's tallest texels push through neighbouring layers where they meet; 0 blends by weight alone.")]
        [Range(0f, 2f)]
        public float heightBlend = 1f;

        [Tooltip("How much the map's macro tint and the broad brightness variation colour this layer.")]
        [Range(0f, 1f)]
        public float macroStrength = 1f;

        [Tooltip("How far hex tiling may turn each cell of the texture so it never visibly repeats: 1 is any angle, 0 none. Row layers never turn, and their cells shift only along the rows.")]
        [Range(0f, 1f)]
        public float antiTiling = 1f;

        string IMapKeyed.Key => key;

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs)
        {
            this.key = key;
            terrainLayer = refs.Asset<TerrainLayer>(refs.terrainLayer, "terrainLayer");
        }
    }
}
