using System;
using HyperRTS.Editor.Maps.Library;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A shelterbelt or hedgerow: rows of trees with shrubs in the gaps and on the edges.</summary>
    [Serializable]
    public sealed class MapBeltKit : IMapKeyed, IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [Tooltip("Full width across the belt, shrub fringe included (metres).")]
        public float width = 8f;

        [Min(1)]
        public int rows = 2;

        [Tooltip("Distance between trees along a row (metres).")]
        public float spacing = 5f;

        public List<MapTreeShare> trees = new();

        [Tooltip("Climate tree (shrub) filling the gaps and edges; empty for none.")]
        [MapKey(typeof(MapClimateTree))]
        public string shrub;

        [Range(0f, 1f)]
        public float shrubShare = 0.3f;

        [Tooltip("Ground under the belt.")]
        [MapKey(typeof(MapClimateSurface))]
        public string surface;

        [Tooltip("A gap every this many metres along the belt (0 for none), as farm tracks cut through belts.")]
        public float gapEvery = 140f;

        public float gapWidth = 9f;

        string IMapKeyed.Key => key;

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs) => this.key = key;
    }
}
