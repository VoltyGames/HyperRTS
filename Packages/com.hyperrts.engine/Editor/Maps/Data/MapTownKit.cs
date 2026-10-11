using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>The buildings of a settlement type and how they line the streets.</summary>
    [Serializable]
    public sealed class MapTownKit
    {
        public string key;

        [Tooltip("Palette keys, picked in turn with some shuffling.")]
        public List<string> buildings = new();

        [Tooltip("Distance between building plots along a road (metres).")]
        public float spacing = 18f;

        [Tooltip("Distance from the road centre to the building's centre (metres).")]
        public float setback = 12f;
    }
}
