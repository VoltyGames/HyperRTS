using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A landscape kit named by a spec's <c>climate</c>, read from the library's <c>&lt;Key&gt;.climate.json</c>.</summary>
    [Serializable]
    [MapLibraryItem("climate", "surfaces", "roadClasses", "farmlandKits", "beltKits", "woodlandKits", "water", "weathers")]
    public sealed class MapClimate : IMapLibraryItem
    {
        [NonSerialized]
        public string name;

        /// <summary>The library file it was read from. Generated terrain textures go beside it.</summary>
        [NonSerialized]
        public string file;

        [Tooltip("Terrain layers (up to 16), blended by the HyperRTS terrain shader.")]
        [NonSerialized]
        public List<MapClimateLayer> layers = new();

        [Tooltip("Named ground types (Meadow, Wheat, ForestFloor) that land use and spec areas paint.")]
        [NonSerialized]
        public List<MapClimateSurface> surfaces = new();

        [NonSerialized]
        public List<MapClimateTree> trees = new();

        [Tooltip("Grass and small plants. Surfaces say where they grow.")]
        [NonSerialized]
        public List<MapClimatePlant> plants = new();

        [Tooltip("Crops grown in 3D rows. Surfaces say where they grow.")]
        [NonSerialized]
        public List<MapClimateRowCrop> rowCrops = new();

        [Tooltip("Ground decals. Surfaces and props say where they lie.")]
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

        [Tooltip("Surface of the ground around each base.")]
        [MapKey(typeof(MapClimateSurface))]
        public string baseSurface;

        [Tooltip("Surface on steep ground, fading in from Slope Start to Slope Full degrees.")]
        [MapKey(typeof(MapClimateSurface))]
        public string slopeSurface;

        public float slopeStart = 22f;
        public float slopeFull = 34f;

        [Tooltip("Texel height difference (0..1) over which one layer takes over from another where they mix; smaller interlocks borders more.")]
        [Range(0.02f, 0.5f)]
        public float heightBlendDepth = 0.06f;

        public MapClimateGroundTint groundTint = new();

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

        [Tooltip("Plants of the shallows along the banks, such as reeds. Empty for none.")]
        [MapKey(typeof(MapClimatePlant))]
        public string reedPlant;

        [Tooltip("Width of the reed band reaching out from the water's edge (metres).")]
        public float reedWidth = 3f;

        [Tooltip("Metres past a ford's edge over which the river deepens again.")]
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
        [Tooltip("Material of river surfaces. The mesh's V coordinate runs downstream in metres.")]
        [NonSerialized]
        public Material water;

        [Tooltip("Weathers a map or match can pick, each an atmosphere prefab (sky, sun, fog, post-processing).")]
        [NonSerialized]
        public List<MapClimateWeather> weathers = new();

        [Header("Distances")]
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

        public int PlantIndex(string key) => MapKeys.IndexOf(plants, key, name, "plant");

        public int RowCropIndex(string key) => MapKeys.IndexOf(rowCrops, key, name, "row crop");

        public bool HasTree(string key) => MapKeys.Contains(trees, key);

        public MapClimateWeather Weather(string key) => MapKeys.Get(weathers, key, name, "weather");

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs)
        {
            name = key;
            file = refs.file;
            water = refs.Asset<Material>(refs.water, "water");
            weathers = refs.weathers.ConvertAll(weather => new MapClimateWeather
            {
                key = weather.key,
                atmosphere = refs.Asset<GameObject>(weather.atmosphere, $"weathers '{weather.key}' atmosphere"),
            });
        }
    }
}
