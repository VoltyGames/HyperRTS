using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>One sown field as the GPU reads it (32 bytes, matches RowCrops.compute).</summary>
    [Serializable]
    public struct RowCropField
    {
        public const int Stride = 32;

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
    }
}
