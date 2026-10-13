using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// A landscape kit named by a spec's <c>climate</c>: terrain layers, the surfaces land is painted with, trees,
    /// plants, how roads, farmland, belts and woods look, water and the sky. Read from the map library's
    /// <c>&lt;Key&gt;.climate.json</c>, which lists its surfaces, road classes and kits by key; the layers, trees,
    /// plants and decals are the ones those (and the props) use (see <see cref="MapLibrary.Climate"/>).
    /// </summary>
    [Serializable]
    [MapLibraryItem("climate", "surfaces", "roadClasses", "farmlandKits", "beltKits", "woodlandKits", "water", "atmosphere", "skybox")]
    public sealed class MapClimate : IMapLibraryItem
    {
        [NonSerialized]
        public string name;

        /// <summary>The library file it was read from; generated terrain textures go in a folder beside it.</summary>
        [NonSerialized]
        public string file;

        [Tooltip("Terrain layers (up to 16), blended by the HyperRTS terrain shader.")]
        [NonSerialized]
        public List<MapClimateLayer> layers = new();

        [Tooltip("Named ground types (Meadow, Wheat, ForestFloor); land use and spec areas paint these.")]
        [NonSerialized]
        public List<MapClimateSurface> surfaces = new();

        [NonSerialized]
        public List<MapClimateTree> trees = new();

        [Tooltip("Grass and small plants; surfaces say where they grow.")]
        [NonSerialized]
        public List<MapClimateDetail> details = new();

        [Tooltip("Crops grown in 3D rows; surfaces say where they grow.")]
        [NonSerialized]
        public List<MapClimateRowCrop> rowCrops = new();

        [Tooltip("Ground decals; surfaces and props say where they lie.")]
        [NonSerialized]
        public List<MapClimateDecal> decals = new();

        [Header("Land")]
        [NonSerialized]
        public List<MapRoadClass> roadClasses = new();

        [NonSerialized]
        public List<MapFarmlandKit> farmlandKits = new();

        [NonSerialized]
        public List<MapBeltKit> beltKits = new();

        [NonSerialized]
        public List<MapWoodlandKit> woodlandKits = new();

        [Header("Ground")]
        [Tooltip("Surface of open ground nothing else claims.")]
        [MapKey(typeof(MapClimateSurface))]
        public string groundSurface;

        [Tooltip("Surface patched into open ground by noise, so it never reads as one flat texture.")]
        [MapKey(typeof(MapClimateSurface))]
        public string groundVariation;

        [Range(0f, 1f)]
        public float variationCoverage = 0.35f;

        [Tooltip("Surface of the ground around each base.")]
        [MapKey(typeof(MapClimateSurface))]
        public string baseSurface;

        [Tooltip("Surface on steep ground; it fades in from Slope Start to Slope Full degrees.")]
        [MapKey(typeof(MapClimateSurface))]
        public string slopeSurface;

        public float slopeStart = 22f;
        public float slopeFull = 34f;

        [Header("Rivers and ravines")]
        [Tooltip("Surface of river banks just above the water.")]
        [MapKey(typeof(MapClimateSurface))]
        public string shoreSurface;

        [Tooltip("Width of the bank band above the water line (metres).")]
        public float shoreWidth = 6f;

        [Tooltip("Surface of the floodplain meadows beyond the banks.")]
        [MapKey(typeof(MapClimateSurface))]
        public string floodplainSurface;

        public float floodplainWidth = 18f;

        [Tooltip("Plants of the shallows along the banks (reeds); empty for none.")]
        [MapKey(typeof(MapClimateDetail))]
        public string reedDetail;

        [Tooltip("Width of the reed band reaching out from the water's edge (metres).")]
        public float reedWidth = 3f;

        [Tooltip("Metres past a ford's edge over which the river deepens again (water thins and foams over this band).")]
        public float fordShallowsWidth;

        [Tooltip("Metres of fast, foaming water downstream of a ford.")]
        public float fordRapidsLength;

        [Tooltip("Surface inside ravines (floor and walls).")]
        [MapKey(typeof(MapClimateSurface))]
        public string ravineSurface;

        [Tooltip("Surface of the gentle rim slopes outside ravine cuts.")]
        [MapKey(typeof(MapClimateSurface))]
        public string ravineRimSurface;

        [Header("Water and sky")]
        [Tooltip("Material of river surfaces; the mesh's V coordinate runs downstream in metres.")]
        [NonSerialized]
        public Material water;

        [Tooltip("Sun, post-processing volume and other lighting, instantiated into every map of this climate.")]
        [NonSerialized]
        public GameObject atmosphere;

        [NonSerialized]
        public Material skybox;
        public Color fogColor = new(0.72f, 0.76f, 0.8f);
        public float fogStart = 150f;
        public float fogEnd = 600f;

        [Header("Distances")]
        public float detailDistance = 120f;
        public float treeDistance = 600f;

        public MapClimateSurface Surface(string key) => MapKeys.Get(surfaces, key, name, "surface");

        public MapRoadClass RoadClass(string key) => MapKeys.Get(roadClasses, key, name, "road class");

        public MapFarmlandKit FarmlandKit(string key) => MapKeys.Get(farmlandKits, key, name, "farmland kit");

        public MapBeltKit BeltKit(string key) => MapKeys.Get(beltKits, key, name, "belt kit");

        public MapWoodlandKit WoodlandKit(string key) => MapKeys.Get(woodlandKits, key, name, "woodland kit");

        /// <summary>The terrain layer a surface paints.</summary>
        public int SurfaceLayer(string surface) => LayerIndex(Surface(surface).layer);

        public int LayerIndex(string key) => MapKeys.IndexOf(layers, key, name, "layer");

        public MapClimateDecal Decal(string key) => MapKeys.Get(decals, key, name, "decal");

        public int TreeIndex(string key) => MapKeys.IndexOf(trees, key, name, "tree");

        public int DetailIndex(string key) => MapKeys.IndexOf(details, key, name, "detail");

        public int RowCropIndex(string key) => MapKeys.IndexOf(rowCrops, key, name, "row crop");

        public bool HasTree(string key) => MapKeys.Contains(trees, key);

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs)
        {
            name = key;
            file = refs.file;
            water = refs.Asset<Material>(refs.water, "water");
            atmosphere = refs.Asset<GameObject>(refs.atmosphere, "atmosphere");
            skybox = refs.Asset<Material>(refs.skybox, "skybox");
        }
    }
}
