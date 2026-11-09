using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Vegetation.Grass
{
    /// <summary>One scattered plant, written by Grass.compute and drawn by <c>Hidden/HyperRTS/FoliageGrass</c>.</summary>
    [GenerateHLSL(needAccessors: false)]
    public struct GrassInstance
    {
        public Vector3 position;
        public float width;
        public float height;
        public float yaw;

        /// <summary>XZ direction times the sine of the tilt.</summary>
        public Vector2 lean;

        /// <summary>Ground colour under the plant over the colour round it.</summary>
        public Vector3 ground;

        public float shade;
    }
}
