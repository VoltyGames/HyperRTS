using HyperRTS.Simulation.Common.Authoring;
using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>The horizon's open ground: a height that swells gently with noise, as the map's own relief does.</summary>
    [Serializable]
    public sealed class HorizonRelief
    {
        [Tooltip("World height of open ground.")]
        public float height;

        [Tooltip("How far the ground swells above and below its height.")]
        [Unit("m")]
        [Min(0f)]
        public float swell = 3f;

        [Tooltip("Wavelength of the swells.")]
        [Unit("m")]
        [Min(1f)]
        public float scale = 300f;

        [Tooltip("Metres past the terrain's edge over which its heights give way to open ground.")]
        [Min(1f)]
        public float blend = 160f;

        /// <summary>Open ground's world height at a point.</summary>
        public float Open(Vector2 point, int seed) =>
            height + (Mathf.PerlinNoise((point.x + seed % 997) / scale, point.y / scale) * 2f - 1f) * swell;
    }
}
