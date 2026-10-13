using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// One sown field as the GPU reads it (32 bytes, the layout RowCrops.compute declares): its row lattice in world
    /// XZ and the crop. Symmetric copies of a field get the copy's turned or mirrored lattice and the same seed, so
    /// both sides grow the same plants.
    /// </summary>
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

        [Tooltip("Picks each plant's jitter, size and turn; the same in every symmetric copy.")]
        public int seed;
    }
}
