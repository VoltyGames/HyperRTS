using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// A ground kit named by a spec's <c>climate</c>: terrain layers, the surfaces areas and roads paint, trees,
    /// grass, water and the sky. The game owns one asset per climate, named after the key.
    /// </summary>
    [CreateAssetMenu(menuName = "HyperRTS/Maps/Map Climate", fileName = "Climate")]
    public sealed class MapClimate : ScriptableObject
    {
        [Tooltip("Terrain layers; at most 8 keep the terrain to two shader passes.")]
        public List<MapClimateLayer> layers = new();

        [Tooltip("Named ground types (Meadow, WheatField, Forest, Asphalt); spec areas and roads use these keys.")]
        public List<MapClimateSurface> surfaces = new();

        public List<MapClimateTree> trees = new();

        [Tooltip("Grass and small plants painted with the terrain layers.")]
        public List<MapClimateDetail> details = new();

        [Header("Ground")]
        [Tooltip("Surface of open ground no area covers.")]
        public string groundSurface;

        [Tooltip("Surface patched into open ground by noise, so it never reads as one flat texture.")]
        public string groundVariation;

        [Range(0f, 1f)]
        public float variationCoverage = 0.35f;

        [Tooltip("Surface on steep ground; it fades in from Slope Start to Slope Full degrees.")]
        public string slopeSurface;

        public float slopeStart = 22f;
        public float slopeFull = 34f;

        [Tooltip("Surface of river banks just above the water.")]
        public string shoreSurface;

        [Tooltip("Width of the shore band above the water line (metres).")]
        public float shoreWidth = 6f;

        [Tooltip("Surface inside ravines (floor and walls).")]
        public string ravineSurface;

        [Tooltip("Tree planted in the yards behind town buildings; empty for none.")]
        public string gardenTree;

        [Header("Water and sky")]
        [Tooltip("Material of river surfaces; the mesh's V coordinate runs downstream in metres.")]
        public Material water;

        [Tooltip("Sun, post-processing volume and other lighting, instantiated into every map of this climate.")]
        public GameObject atmosphere;

        public Material skybox;
        public Color fogColor = new(0.72f, 0.76f, 0.8f);
        public float fogStart = 150f;
        public float fogEnd = 600f;

        [Header("Distances")]
        public float detailDistance = 120f;
        public float treeDistance = 600f;

        public MapClimateSurface Surface(string key) => surfaces.Find(s => s.key == key);

        public int LayerIndex(string key) => layers.FindIndex(l => l.key == key);

        public int TreeIndex(string key) => trees.FindIndex(t => t.key == key);

        public int DetailIndex(string key) => details.FindIndex(d => d.key == key);
    }
}
