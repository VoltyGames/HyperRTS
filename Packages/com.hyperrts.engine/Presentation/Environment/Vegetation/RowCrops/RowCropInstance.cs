using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>One planted row crop, written by RowCrops.compute and drawn by RowCrop.shader.</summary>
    [GenerateHLSL(needAccessors: false)]
    public struct RowCropInstance
    {
        public Vector3 position;
        public float scale;
        public Vector2 facing;
        public Vector2 trampleDirection;
        public float bend;
        public float shade;

        /// <summary>XZ direction times the sine of the tilt.</summary>
        public Vector2 lean;
    }
}
