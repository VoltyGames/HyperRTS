using System;
using HyperRTS.Presentation.Environment.Vegetation.RowCrops;
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

        [Tooltip("Row crop whose canopy raises the fields that grow it; empty for flat ground.")]
        public RowCrop crop;

        /// <summary>Whether fields of this crop stand raised, drawn with its row crop's canopy.</summary>
        public bool Raised => crop != null && crop.canopyMaterial != null;
    }
}
