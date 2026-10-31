using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>A kind of wood: canopy trees, a shrubby edge, clearings and the forest floor.</summary>
    [MapLibraryItem("woodland")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Woodland Kit", fileName = "New.woodland")]
    public sealed class MapWoodlandKit : MapLibraryItem
    {
        [Tooltip("Forest floor.")]
        [MapKey(typeof(MapClimateSurface))]
        public string surface;

        [Tooltip("Canopy trees per hectare.")]
        public List<MapTreeDensity> trees = new();

        [Tooltip("Smallest distance between canopy trees (metres).")]
        public float spacing = 4.5f;

        [Tooltip("Band inside the wood's outline where shrubs and young trees grow (metres).")]
        public float edgeWidth = 6f;

        [MapKey(typeof(MapClimateSurface))]

        public string edgeSurface;

        [Tooltip("Shrubs and young trees per hectare in the edge band.")]
        public List<MapTreeDensity> edgeTrees = new();

        [Tooltip("Share of the wood left as open glades.")]
        [Range(0f, 0.6f)]
        public float clearings = 0.12f;
    }
}
