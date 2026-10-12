using System;
using HyperRTS.Editor.Maps.Library;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A named ground type: the layer it paints, and the trees, plants and props that grow or lie on it.</summary>
    [Serializable]
    [MapLibraryItem("surface")]
    public sealed class MapClimateSurface : IMapKeyed, IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [Tooltip("Key of the terrain layer this surface paints.")]
        [MapKey(typeof(MapClimateLayer))]
        public string layer;

        public List<MapTreeDensity> trees = new();

        [Tooltip("Smallest distance between trees on this surface (metres).")]
        public float treeSpacing = 4f;

        [Tooltip("Grass and small plants and the share of the ground each covers.")]
        public List<MapDetailCoverage> details = new();

        public List<MapPropDensity> props = new();

        string IMapKeyed.Key => key;

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs) => this.key = key;
    }
}
