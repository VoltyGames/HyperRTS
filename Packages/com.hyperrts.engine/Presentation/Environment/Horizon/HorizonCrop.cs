using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>One crop of the horizon's fields: its terrain layer and how often a field grows it.</summary>
    [Serializable]
    public struct HorizonCrop
    {
        [Tooltip("Terrain layer in the map terrain material's layer arrays.")]
        [Min(0)]
        public int layer;

        [Min(0f)]
        public float weight;
    }
}
