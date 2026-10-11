using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// A landscape kit named by a spec's <c>climate</c>: terrain layers, the surfaces land is painted with, trees,
    /// plants, how roads, farmland, belts and woods look, water and the sky. The game owns one asset per climate,
    /// named after the key.
    /// </summary>
    [CreateAssetMenu(menuName = "HyperRTS/Maps/Map Climate", fileName = "Climate")]
    public sealed class MapClimate : ScriptableObject
    {
        [Tooltip("Terrain layers (up to 16), blended by the HyperRTS terrain shader.")]
        public List<MapClimateLayer> layers = new();

        [Tooltip("Named ground types (Meadow, Wheat, ForestFloor); land use and spec areas paint these.")]
        public List<MapClimateSurface> surfaces = new();

        public List<MapClimateTree> trees = new();

        [Tooltip("Grass and small plants; surfaces say where they grow.")]
        public List<MapClimateDetail> details = new();

        [Header("Land")]
        public List<MapRoadClass> roadClasses = new();
        public List<MapFarmlandKit> farmlandKits = new();
        public List<MapBeltKit> beltKits = new();
        public List<MapWoodlandKit> woodlandKits = new();

        [Header("Ground")]
        [Tooltip("Surface of open ground nothing else claims.")]
        public string groundSurface;

        [Tooltip("Surface patched into open ground by noise, so it never reads as one flat texture.")]
        public string groundVariation;

        [Range(0f, 1f)]
        public float variationCoverage = 0.35f;

        [Tooltip("Surface of the ground around each base.")]
        public string baseSurface;

        [Tooltip("Surface on steep ground; it fades in from Slope Start to Slope Full degrees.")]
        public string slopeSurface;

        public float slopeStart = 22f;
        public float slopeFull = 34f;

        [Header("Rivers and ravines")]
        [Tooltip("Surface of river banks just above the water.")]
        public string shoreSurface;

        [Tooltip("Width of the bank band above the water line (metres).")]
        public float shoreWidth = 6f;

        [Tooltip("Surface of the floodplain meadows beyond the banks.")]
        public string floodplainSurface;

        public float floodplainWidth = 18f;

        [Tooltip("Plants of the shallows along the banks (reeds); empty for none.")]
        public string reedDetail;

        [Tooltip("Width of the reed band reaching out from the water's edge (metres).")]
        public float reedWidth = 3f;

        [Tooltip("Surface inside ravines (floor and walls).")]
        public string ravineSurface;

        [Tooltip("Surface of the gentle rim slopes outside ravine cuts.")]
        public string ravineRimSurface;

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

        public MapRoadClass RoadClass(string key) => roadClasses.Find(r => r.key == key);

        public MapFarmlandKit FarmlandKit(string key) => farmlandKits.Find(k => k.key == key);

        public MapBeltKit BeltKit(string key) => beltKits.Find(k => k.key == key);

        public MapWoodlandKit WoodlandKit(string key) => woodlandKits.Find(k => k.key == key);

        public int LayerIndex(string key) => layers.FindIndex(l => l.key == key);

        public int TreeIndex(string key) => trees.FindIndex(t => t.key == key);

        public int DetailIndex(string key) => details.FindIndex(d => d.key == key);
    }
}
