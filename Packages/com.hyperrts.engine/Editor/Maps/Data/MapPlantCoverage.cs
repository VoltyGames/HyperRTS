using System;
using UnityEngine;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// How much of a surface one plant (grass, flowers, weeds) covers, and whether it spreads evenly like a sward or
    /// grows in stands of its own.
    /// </summary>
    [Serializable]
    public sealed class MapPlantCoverage
    {
        [MapKey(typeof(MapClimatePlant))]
        public string plant;

        [Tooltip("Share of the ground the plant covers where it grows, 0 to 1.")]
        [Range(0f, 1f)]
        public float coverage = 0.5f;

        [Tooltip("Share of the surface inside the plant's stands, 0 to 1; 1 spreads it over the whole surface.")]
        [Range(0f, 1f)]
        public float stands = 1f;

        [Tooltip("Metres across the largest stands; the noise adds smaller ones down to an eighth of that.")]
        [Min(1f)]
        public float standSize = 14f;
    }
}
