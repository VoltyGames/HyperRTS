using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>A named ground type: its terrain layer and what grows or lies on it.</summary>
    [MapLibraryItem("surface")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Surface", fileName = "New.surface")]
    public sealed class MapClimateSurface : MapLibraryItem
    {
        [Tooltip("Key of the terrain layer this surface paints.")]
        [MapKey(typeof(MapClimateLayer))]
        public string layer;

        [Tooltip("SurfaceMaterial name hits and wheels on this surface throw up (Soil, Grass, Mud, Gravel...).")]
        public string impactClass;

        public List<MapTreeDensity> trees = new();

        [Tooltip("Smallest distance between trees on this surface (metres).")]
        public float treeSpacing = 4f;

        [Tooltip("Grass and small plants and the share of the ground each covers.")]
        public List<MapPlantCoverage> plants = new();

        [Tooltip("Crop grown in 3D rows on this surface's fields. Empty for none.")]
        [MapKey(typeof(MapClimateRowCrop))]
        public string rowCrop;

        public List<MapPropDensity> props = new();

        [Tooltip("Ground decals laid on this surface at build time, per hectare.")]
        public List<MapDecalDensity> decals = new();

        [Tooltip("Noise-shaped mixes of other terrain layers, painted in order over this surface's own.")]
        public List<MapSurfaceMix> mixes = new();

        [Tooltip("Wheel lines along the rows of this surface's fields.")]
        public MapTramlines tramlines = new();

        [Tooltip("Lighter stripes a harvest leaves along the rows of this surface's fields.")]
        public MapSwaths swaths = new();

        [Header("Edge")]
        [Tooltip("Width of the band over which this surface blends into its neighbour (metres). Where two surfaces meet, the narrower width wins.")]
        public float edgeWidth;

        [Tooltip("How far the edge line wanders (metres); larger wander also waves more slowly. The smaller of the two sides wins.")]
        public float edgeWander;
    }
}
