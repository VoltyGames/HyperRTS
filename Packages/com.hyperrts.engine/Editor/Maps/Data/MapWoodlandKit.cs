using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A kind of wood: canopy trees, a shrubby edge, clearings and the forest floor.</summary>
    [Serializable]
    public sealed class MapWoodlandKit : IMapKeyed
    {
        public string key;

        [Tooltip("Forest floor.")]
        public string surface;

        [Tooltip("Canopy trees per hectare.")]
        public List<MapTreeDensity> trees = new();

        [Tooltip("Smallest distance between canopy trees (metres).")]
        public float spacing = 4.5f;

        [Tooltip("Band inside the wood's outline where shrubs and young trees grow (metres).")]
        public float edgeWidth = 6f;

        public string edgeSurface;

        [Tooltip("Shrubs and young trees per hectare in the edge band.")]
        public List<MapTreeDensity> edgeTrees = new();

        [Tooltip("Share of the wood left as open glades.")]
        [Range(0f, 0.6f)]
        public float clearings = 0.12f;

        string IMapKeyed.Key => key;
    }
}
