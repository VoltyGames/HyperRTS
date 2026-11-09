using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>One sown field as the GPU reads it (RowCrops.compute includes the generated layout).</summary>
    [Serializable]
    [GenerateHLSL(needAccessors: false)]
    public struct RowCropField
    {
        [Tooltip("World XZ of lattice spot (0, 0).")]
        public Vector2 origin;

        [Tooltip("Unit world XZ direction the rows run.")]
        public Vector2 along;

        [Tooltip("Unit world XZ direction from one row to the next.")]
        public Vector2 across;

        [Tooltip("Index into the field asset's crops.")]
        public int crop;

        [Tooltip("Seeds each plant's jitter, size and turn. Same in every symmetric copy.")]
        public int seed;

        [Tooltip("Wheel lines the crop leaves bare.")]
        public RowCropTramlines tramlines;
    }
}
