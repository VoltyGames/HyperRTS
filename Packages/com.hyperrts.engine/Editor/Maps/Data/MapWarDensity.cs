using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How thickly one war age scars the open land of a map.</summary>
    [Serializable]
    public sealed class MapWarDensity
    {
        [Min(0f)]
        public float cratersPerHectare;

        [Min(0f)]
        public float burnsPerHectare;

        [Min(0f)]
        public float wrecksPerHectare;

        [Min(0f)]
        public float trenchesPerHectare;

        [Tooltip("Share of belt and wood-edge trees swapped for their shredded species.")]
        [Range(0f, 1f)]
        public float shreddedShare;
    }
}
