using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A named ground type: the layer it paints and the trees that grow on it.</summary>
    [Serializable]
    public sealed class MapClimateSurface
    {
        public string key;

        [Tooltip("Key of the terrain layer this surface paints.")]
        public string layer;

        public List<MapTreeDensity> trees = new();

        [Tooltip("Smallest distance between trees on this surface (metres).")]
        public float treeSpacing = 4f;
    }
}
