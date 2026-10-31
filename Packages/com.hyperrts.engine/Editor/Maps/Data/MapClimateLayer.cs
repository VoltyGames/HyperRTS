using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A terrain texture layer, how the terrain shader lays it on the ground, and the grass that grows wherever it is painted.</summary>
    [MapLibraryItem("layer")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Layer", fileName = "New.layer")]
    public sealed class MapClimateLayer : MapLibraryItem
    {
        [Tooltip("Diffuse, normal and mask maps (mask: occlusion G, height B, smoothness A), tile size, normal scale, tint (Diffuse Remap Max) and mask remap.")]
        public TerrainLayer terrainLayer;

        [Tooltip("Colour of this ground in the report's overview image.")]
        public Color mapColor = Color.grey;

        [Tooltip("Turns the texture clockwise on the ground (degrees); at 0 the image's top faces north.")]
        public float rotation;

        [Tooltip("Turns the texture per pixel to its own field's or garden's rows; Rotation must lay the image's lines along them (0 for lines running left to right, 90 for top to bottom).")]
        public bool followsRows;

        [Tooltip("How far the texture's tallest texels push through neighbouring layers where they meet; 0 blends by weight alone.")]
        [Range(0f, 2f)]
        public float heightBlend = 1f;

        [Tooltip("How much the map's ground tint and the broad brightness variation colour this layer.")]
        [Range(0f, 1f)]
        public float tintStrength = 1f;

        [Tooltip("How far hex tiling may turn each cell of the texture so it never visibly repeats: 1 is any angle, 0 none. Row layers never turn, and their cells shift only along the rows.")]
        [Range(0f, 1f)]
        public float antiTiling = 1f;

        [Tooltip("How much the layer is grass cover that sheds rain instead of darkening and puddling: 1 is all grass, 0 bare ground.")]
        [Range(0f, 1f)]
        public float grass;

        [Tooltip("Share of their cover that scattered plants keep where this layer is painted, such as few stalks on burnt ground.")]
        [Range(0f, 1f)]
        public float plants = 1f;
    }
}
